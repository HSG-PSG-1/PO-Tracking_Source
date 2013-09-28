using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using POT.DAL;
using POT.Services;
using HSG.Helper;

namespace POT.Controllers
{
    //[CompressFilter] - don't use it here
    [IsAuthorize(IsAuthorizeAttribute.Rights.NONE)]//Special case for some dirty session-abandoned pages and hacks
    public partial class POController : BaseController
    {
        #region Actions for PO (Secured)

        [AccessPO("POID")]
        [CacheControl(HttpCacheability.NoCache), HttpGet]
        public ActionResult Manage(int POID, bool? printPOAfterSave)
        {
            ViewData["oprSuccess"] = base.operationSuccess; //oprSuccess will be reset after this
            ViewData["printPOAfterSave"] = (TempData["printPOAfterSave"] ?? false);

            #region Edit mode
            #region Get PO view and check if its empty or archived - redirect

            vw_POHeader vw = new POService().GetPOHeaderById(POID);

            if (vw.ID <= Defaults.Integer && (vw.OrderStatusID == null || vw.OrderStatusID == Defaults.Integer))
            {
                ViewData["Message"] = "PO not found"; return View("DataNotFound"); /* deleted po accessed from Log*/
            }
            //// In case an archived entry is accessed
            //if (vw.Archived)
            //    return RedirectToAction("Archived", new { POID = POID });
            //Empty so invalid POID - go to Home
            if (vw == new POService().emptyView)
                return RedirectToAction("List", "Dashboard");

            #endregion

            //Reset the Session PO object
            POHeader poObj = POService.GetPOObjFromVW(vw);
            //_Session.PO = poObj;
            //_Session.POs[poObj.POGUID] = poObj;// Populate original obj

            POHdrKOModel vmPO = doAddEditPopulateKO(vw);
            return View(vmPO);
            #endregion
        }

        [HttpPost]
        [AccessPO("POID")]
        public ActionResult Delete(int POID, string POGUID, string PONumber)
        {
            //http://www.joe-stevens.com/2010/02/16/creating-a-delete-link-with-mvc-using-post-to-avoid-security-issues/
            //http://stephenwalther.com/blog/archive/2009/01/21/asp.net-mvc-tip-46-ndash-donrsquot-use-delete-links-because.aspx
            //Anti-FK: http://blog.codeville.net/2008/09/01/prevent-cross-site-request-forgery-csrf-using-aspnet-mvcs-antiforgerytoken-helper/

            #region Delete po & log activity

            new POService().Delete(new POHeader() { ID = POID });
            //Log Activity (before directory del and sesion clearing)
            new ActivityLogService(ActivityLogService.Activity.PODelete).Add(
                new ActivityHistory() { POID = POID, PONumber = PONumber });

            #endregion

            // Make sure the PREMANENT files are also deleted
            FileIO.EmptyDirectory(FileIO.GetPODirPathForDelete(POID, null, null, false));
            // Reset PO in session
            _Session.ResetPOInSessionAndEmptyTempUpload(POGUID);

            return Redirect("~/Dashboard");
        }

        public ActionResult Cancel(int POID, string POGUID)
        {
            // Make sure the temp files are also deleted
            FileIO.EmptyDirectory(FileIO.GetPOFilesTempFolder(POGUID, true));
            FileIO.EmptyDirectory(FileIO.GetPOFilesTempFolder(POGUID, false));            

            _Session.ResetPOInSessionAndEmptyTempUpload(POGUID);
            return Redirect("~/Dashboard");
        }

        [HttpPost]
        [AccessPO("POID")]
        public ActionResult Manage(int POID, bool isAddMode, [FromJson]POHeader poObj, [FromJson] IEnumerable<POComment> comments,
            [FromJson] IEnumerable<POFile> files, /*int OrderStatusIDold,*/ bool? printPOAfterSave)
        {
            bool success = false;
            //return new JsonResult() { Data = new{ msg = "success"}};
            
            //HT: Note the following won't work now as we insert a record in DB then get it back in edit mode for Async edit
            //bool isAddMode = (poObj.ID <= Defaults.Integer); 

            #region Perform operation proceed and set result

            string result = new PAWPO(false).AsyncBulkAddEditDelKO(poObj, poObj.OrderStatusIDold??-1 /*OrderStatusIDold*/, comments, files);
            success = !string.IsNullOrEmpty(result);

            if (!success) {/*return View(poObj);*/}
            else //Log Activity based on mode
            {
                poObj.PONumber = result;// Set PO #
                ActivityLogService.Activity act = /*isAddMode ? ActivityLogService.Activity.POAdd :*/ ActivityLogService.Activity.POEdit;
                new ActivityLogService(act).Add(new ActivityHistory() { POID = poObj.ID, PONumber = poObj.PONumber.ToString() });
            }

            #endregion

            base.operationSuccess = success;//Set opeaon success
            _Session.ResetPOInSessionAndEmptyTempUpload(poObj.POGUID); // reset because going back to Manage will automatically creat new session
            
            if(success)
                TempData["printPOAfterSave"] = printPOAfterSave.HasValue && printPOAfterSave.Value;
            
            return RedirectToAction("Manage", new { POID = poObj.ID });
        }
        #endregion   

        [AccessPO("POID")]
        [CacheControl(HttpCacheability.NoCache), HttpGet]
        public ActionResult Lines(int POID, string POGUID)
        {            
            return View(new DetailService().Search(POID, null));            
        }

        [HttpPost]
        public ActionResult Navigate(int POID, string NavString)
        {
            object dashboardFilter = _Session.Search[Filters.list.Dashboard];
            vw_PO_Dashboard searchOpts = (dashboardFilter != null) ? (vw_PO_Dashboard)dashboardFilter : new DashboardService().emptyView;
            List<int> POIDs = _Session.POIDs;// new DashboardService().SearchPOIDKO(searchOpts);

            if (POIDs == null)
                return RedirectToAction("Manage", new { POID = POID });

            int pos = _Session.POposition(POID);// POIDs.FindIndex(i => i == POID);

            switch (NavString.ToUpper())
            {
                case "FIRST": POID = POIDs[0]; break;
                case "PREV": if(pos-1 <= 0) pos = 1; POID = POIDs[pos-1]; break;
                case "NEXT": if (pos + 1 > POIDs.Count-1) pos = POIDs.Count-2; POID = POIDs[pos + 1]; break;
                case "LAST": POID = POIDs[POIDs.Count-1]; break;
            }

            return RedirectToAction("Manage", new { POID = POID });
        }

        
        [AccessPO("POID")]
        [CacheControl(HttpCacheability.NoCache), HttpGet]
        public ActionResult Print(int POID)
        {
            POInternalPrint printView = new POInternalPrint();

            List<POComment> comments = new List<POComment>();
            List<POFile> filesH = new List<POFile>();
            List<vw_POLine> items = new List<vw_POLine>();

            #region Fetch PO data and set Viewstate
            vw_POHeader vw = new POService().GetPOByIdForPrint(POID,
                ref comments, ref filesH, ref items, !_Session.IsOnlyVendor);
            
            vw.POGUID = System.Guid.NewGuid().ToString();

            //Set data in View
            ViewData["comments"] = comments;
            //ViewData["filesH"] = filesH; NOT needed yet
            ViewData["items"] = items;

            printView.view = vw;
            printView.comments = comments;
            //printView.filesH = filesH;
            printView.items = items;
            #endregion

            if (vw == null || vw.ID < 1)//Empty so invalid POID - go to Home
                return RedirectToAction("List", "Dashboard");

            if (vw.ID <= Defaults.Integer && vw.OrderStatusID == Defaults.Integer && vw.AssignTo == Defaults.Integer)
            { ViewData["Message"] = "PO not found"; return View("DataNotFound"); }// deleted po accessed from Log
                        
            //Reset the Session PO object
            //PO poObj = POService.GetPOObjFromVW(vw);

            return View(printView);
        }        
        
        [CacheControl(HttpCacheability.NoCache), HttpGet]
        public ActionResult Info(int POID, string POGUID)
        {
            ViewData["POGUID"] = POGUID;
            return View(new POInfoKOModel() { Info = new POHeader() { ID = POID, POGUID = POGUID } }); // set only whats required POService().GetPOInfoById(POID)
        }

        [CacheControl(HttpCacheability.NoCache), HttpGet]
        public JsonResult POInfoKOVM(int POID, string POGUID)
        {
            POHeader poHdr = new POService().GetPOInfoById(POID);
            poHdr.POGUID = POGUID;
            
            POInfoKOModel vmPOInfo = doAddEditPopulateInfoKO(poHdr);
            
            return Json(vmPOInfo, JsonRequestBehavior.AllowGet);
        }                
        
        #region Extra Functions (for PO actions)
        
        public POHdrKOModel doAddEditPopulateKO(vw_POHeader poData)
        {
            POHdrKOModel vm = new POHdrKOModel()
            {
                PO = poData
                //POModel = new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(poData)
            };
            //ViewData["IsEditMode"] = (id != Defaults.Integer);
            vm.PO.AssignTo = (vm.PO.AssignTo ?? -1); // Special case for some unwanted records!
            vm.PO.AssignToOld = (vm.PO.AssignTo??-1);

            /*vm.Statuses = new LookupService().GetLookup(LookupService.Source.Status);
             vm.Brands = !_Session.IsOnlyVendor?new LookupService().GetLookup(LookupService.Source.BrandItems):
                //Special case for Vendor users (they must see only their Brands)
                new LookupService().GetLookup(LookupService.Source.BrandVendorItems,extras:_SessionUsr.OrgID.ToString());
            */
            return vm;
        }

        public POInfoKOModel doAddEditPopulateInfoKO(POHeader poObj)
        {
            poObj.OrderStatusIDold = poObj.OrderStatusID;

            POInfoKOModel vm = new POInfoKOModel()
            {
                Info = poObj,
                Carrier = new LookupService().GetLookup(LookupService.Source.Carrier),
                ContainerType = new LookupService().GetLookup(LookupService.Source.ContainerType),
                Status = new LookupService().GetLookup(LookupService.Source.Status)
            };
            
            return vm;
        }
        
        #endregion
    }
}
namespace POT.DAL
{
    public class POHdrKOModel
    {
        public vw_POHeader PO { get; set; }        
    }

    public class POInfoKOModel
    {
        public POHeader Info { get; set; }
        public IEnumerable Carrier { get; set; }
        public IEnumerable ContainerType { get; set; }
        public IEnumerable Status { get; set; }
    }
}