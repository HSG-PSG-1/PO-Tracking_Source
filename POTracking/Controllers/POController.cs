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

            POHeader po = new POService().GetPOInfoById(POID);// GetPOHeaderById

            if (po.ID <= Defaults.Integer && (po.OrderStatusID == null || po.OrderStatusID == Defaults.Integer))
            {
                ViewData["Message"] = "PO not found"; return View("DataNotFound"); /* deleted po accessed from Log*/
            }
            //// In case an archived entry is accessed
            //if (vw.Archived)
            //    return RedirectToAction("Archived", new { POID = POID });
            //Empty so invalid POID - go to Home
            if (po == new POService().emptyPO)//emptyView
                return RedirectToAction("List", "Dashboard");

            #endregion
            po.POGUID = System.Guid.NewGuid().ToString();
            po.AssignToIDold = po.AssignTo;
            
            return View(po);
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

            string result = new PAWPO(false).AsyncBulkAddEditDelKO(poObj, poObj.OrderStatusIDold ?? -1 /*OrderStatusIDold*/, comments, files);
            success = !string.IsNullOrEmpty(result);

            if (!success) { /*return View(poObj);*/}
            else //Log Activity based on mode
            {
                poObj.PONumber = result;// Set PO #
                ActivityLogService.Activity act = /*isAddMode ? ActivityLogService.Activity.POAdd :*/ ActivityLogService.Activity.POEdit;
                new ActivityLogService(act).Add(new ActivityHistory() { POID = poObj.ID, PONumber = poObj.PONumber.ToString() });
            }

            #endregion

            base.operationSuccess = success;//Set opeaon success
            _Session.ResetPOInSessionAndEmptyTempUpload(poObj.POGUID); // reset because going back to Manage will automatically creat new session

            if (success)
                TempData["printPOAfterSave"] = printPOAfterSave.HasValue && printPOAfterSave.Value;

            if (poObj.AssignTo > 0 && poObj.AssignTo != poObj.AssignToIDold)
                CommentService.SendEmail(POID, poObj.AssignTo.Value, poObj.PONumber, new POComment() { Comment1 = "(no comment)" });

            return RedirectToAction("Manage", new { POID = poObj.ID });
        }
        #endregion

        /*[HttpPost]
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
                case "PREV": if (pos - 1 <= 0) pos = 1; POID = POIDs[pos - 1]; break;
                case "NEXT": if (pos + 1 > POIDs.Count - 1) pos = POIDs.Count - 2; POID = POIDs[pos + 1]; break;
                case "LAST": POID = POIDs[POIDs.Count - 1]; break;
            }

            return RedirectToAction("Manage", new { POID = POID });
        }
        */

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

            //Log Activity
            new ActivityLogService(ActivityLogService.Activity.POPrint).
                Add(new ActivityHistory() { POID = POID, PONumber = vw.PONumber.ToString() });

            return View(printView);
        }

        [CacheControl(HttpCacheability.NoCache), HttpGet]
        public ActionResult Info(int POID, string POGUID)
        {
            ViewData["POGUID"] = POGUID;
            return View(new POInfoKOModel() { Info = new POHeader() { ID = POID, POGUID = POGUID } }); // set only whats required POService().GetPOInfoById(POID)
        }

        [CacheControl(HttpCacheability.NoCache), HttpGet]
        public JsonResult POEditKOViewModel(int POID, string POGUID)
        {// NEW consolidated viewmodel

            POKOViewModel povm = new POKOViewModel(); // Main consolidated viewmodel

            vw_POHeader vwPOHdr = new POService().GetPOHeaderById(POID);// POHeader poObj=POService.GetPOObjFromVW(vwPOHdr); // To set GUID
            POHeader poHdr = new POService().GetPOInfoById(POID);

            
            poHdr.POGUID = POGUID; vwPOHdr.POGUID = POGUID;  

            vwPOHdr = doAddEditPopulateKO(vwPOHdr);

            povm.Header = vwPOHdr;
            povm.Lines = new DetailService().Search(POID, null);

            POInfoKOModel vmPOInfo = doAddEditPopulateInfoKO(poHdr);
            povm.Info = vmPOInfo.Info;

            // For dropdown
            povm.Carrier = vmPOInfo.Carrier;
            povm.Status = vmPOInfo.Status;
            povm.ContainerType = vmPOInfo.ContainerType;

            // Comments
            povm.Comments = GetCommentKOModel(POID, POGUID, poHdr.AssignTo ?? -1);

            // Files
            povm.Files = GetFileKOModel(POID, POGUID);

            // Status History
            povm.StatusHistory = new StatusHistoryService().FetchAll(POID);

            return Json(povm, JsonRequestBehavior.AllowGet);
        }

        #region Extra Functions (for PO actions)

        public vw_POHeader doAddEditPopulateKO(vw_POHeader poData)
        {
            poData.AssignTo = (poData.AssignTo ?? -1); // Special case for some unwanted records!
            poData.AssignToOld = (poData.AssignTo ?? -1);

            poData.POGUID = System.Guid.NewGuid().ToString(); // set unique identifier ID

            /*vm.Statuses = new LookupService().GetLookup(LookupService.Source.Status);
             vm.Brands = !_Session.IsOnlyVendor?new LookupService().GetLookup(LookupService.Source.BrandItems):
                //Special case for Vendor users (they must see only their Brands)
                new LookupService().GetLookup(LookupService.Source.BrandVendorItems,extras:_SessionUsr.OrgID.ToString());
            */
            return poData;
        }

        public POInfoKOModel doAddEditPopulateInfoKO(POHeader poObj)
        {
            poObj.OrderStatusIDold = poObj.OrderStatusID;
            poObj.AssignToIDold = poObj.AssignTo ?? -1;

            POInfoKOModel vm = new POInfoKOModel()
            {
                Info = poObj,
                Carrier = new LookupService().GetLookup(LookupService.Source.Carrier),
                ContainerType = new LookupService().GetLookup(LookupService.Source.ContainerType),
                Status = new LookupService().GetLookup(LookupService.Source.Status)
            };

            return vm;
        }

        public CommentVM GetCommentKOModel(int POID, string POGUID, int AssignTo)
        {
            //Set Comment object
            POComment newObj = new POComment()
            {
                ID = -1,
                _Added = true,
                POID = POID,
                POGUID = POGUID,
                CommentBy = _SessionUsr.UserName,
                LastModifiedBy = _SessionUsr.ID,
                LastModifiedDate = DateTime.Now,
                PostedOn = DateTime.Now,
                UserID = _SessionUsr.ID
            };

            CommentVM vm = new CommentVM()
            {
                CommentToAdd = newObj,
                EmptyComment = newObj,
                AllComments = new CommentService().Search(POID, null),
                AssignTo = AssignTo
            };

            vm.Users = new LookupService().GetLookup(LookupService.Source.User);

            return vm;
        }

        public FileVM GetFileKOModel(int POID, string POGUID)
        {
            //Set File object
            POFile newObj = new POFile()
            {
                ID = -1,
                _Added = true,
                POID = POID,
                POGUID = POGUID,
                UploadedBy = _SessionUsr.UserName,
                LastModifiedBy = _SessionUsr.ID,
                LastModifiedDate = DateTime.Now,
                UploadDate = DateTime.Now,
                UserID = _SessionUsr.ID,
                FileName = "",
                FileNameNEW = ""
            };

            List<POFile> files = new List<POFile>();
            FileVM vm = new FileVM()
            {
                FileToAdd = newObj,
                EmptyFileHeader = newObj,
                AllFiles = (new POFileService().Search(POID, null))
            };
            // Lookup data
            vm.FileTypes = new LookupService().GetLookup(LookupService.Source.POFileType);

            return vm;
        }

        #endregion
    }
}
namespace POT.DAL
{    
    public class POInfoKOModel
    {
        public POHeader Info { get; set; }
        public IEnumerable Carrier { get; set; }
        public IEnumerable ContainerType { get; set; }
        public IEnumerable Status { get; set; }
    }

    public class POKOViewModel
    {
        public vw_POHeader Header { get; set; }
        public List<vw_POLine> Lines { get; set; }
        public string LinesOrderExtTotal { get { return Lines.Sum(l => l.OrderExtension ?? 0.00M).ToString("#0.00"); } }

        public POHeader Info { get; set; }

        // For dropdown
        public IEnumerable Carrier { get; set; }
        public IEnumerable Status { get; set; }
        public IEnumerable ContainerType { get; set; }
        
        // Comments
        public CommentVM Comments { get; set; }

        //Files
        public FileVM Files { get; set; }

        public List<vw_StatusHistory_Usr> StatusHistory { get; set; }
    }
}