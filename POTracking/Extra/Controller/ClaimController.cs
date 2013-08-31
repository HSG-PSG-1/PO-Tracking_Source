using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using POT.DAL;
using POT.Services;
using HSG.Helper;

namespace CPM.Controllers
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
            ViewData["printPOAfterSave"] = (TempData["printPOAfterSave"]??false);

            #region Add mode - add new and return it in editmode
            if (POID <= Defaults.Integer)
            {// HT: CAREFUL: Add mode in which we need to add a new record
                // Also handles special case for customer to set default SP for him
                string spNameForCustomer = string.Empty;
                PO NewPO = new POService().AddDefault(_SessionUsr.ID, _SessionUsr.OrgID, _Session.IsOnlyCustomer, ref spNameForCustomer);
                //Session.POs[NewPO.POGUID] = NewPO;
                //return RedirectToAction("Manage", new { POID = NewPO.ID, POGUID = NewPO.POGUID });
                POKOModel vmPO = doAddEditPopulateKO(POService.GetVWFromPOObj(NewPO, spNameForCustomer));
                return View(vmPO);
            }
            #endregion

            #region Edit mode
            else
            {
                #region Get PO view and check if its empty or archived - redirect
                
                vw_PO_Master_User_Loc vw = new POService().GetPOById(POID);

                if (vw.ID == Defaults.Integer && vw.StatusID == Defaults.Integer)
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
                PO poObj = POService.GetPOObjFromVW(vw);
                //_Session.PO = poObj;
                //_Session.POs[poObj.POGUID] = poObj;// Populate original obj

                POKOModel vmPO = doAddEditPopulateKO(vw);
                return View(vmPO);
            }
            #endregion
        }

        [HttpPost]
        [AccessPO("POID")]
        public ActionResult Delete(int POID, string POGUID, int PONo)
        {
            //http://www.joe-stevens.com/2010/02/16/creating-a-delete-link-with-mvc-using-post-to-avoid-security-issues/
            //http://stephenwalther.com/blog/archive/2009/01/21/asp.net-mvc-tip-46-ndash-donrsquot-use-delete-links-because.aspx
            //Anti-FK: http://blog.codeville.net/2008/09/01/prevent-cross-site-request-forgery-csrf-using-aspnet-mvcs-antiforgerytoken-helper/

            #region Delete po & log activity

            new POService().Delete(new PO() { ID = POID });
            //Log Activity (before directory del and sesion clearing)
            new ActivityLogService(ActivityLogService.Activity.PODelete).Add(
                new ActivityHistory() { POID = POID, PONumber = PONo.ToString() });

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
        public ActionResult Manage(int POID, bool isAddMode,
            [FromJson]vw_PO_Master_User_Loc poObj, [FromJson] IEnumerable<PODetail> items,
            [FromJson] IEnumerable<POComment> comments, [FromJson] IEnumerable<POFile> files, bool? printPOAfterSave)
        {
            bool success = false;
            //return new JsonResult() { Data = new{ msg = "success"}};
            
            //HT: Note the following won't work now as we insert a record in DB then get it back in edit mode for Async edit
            //bool isAddMode = (poObj.ID <= Defaults.Integer); 

            #region Perform operation proceed and set result

            int result = new CAWpo(false).AsyncBulkAddEditDelKO(poObj, poObj.StatusIDold, items, comments, files);
            success = result > 0;

            if (!success) {/*return View(poObj);*/}
            else //Log Activity based on mode
            {
                poObj.PONo = result;// Set PO #
                ActivityLogService.Activity act = isAddMode ? ActivityLogService.Activity.POAdd : ActivityLogService.Activity.POEdit;
                new ActivityLogService(act).Add(new ActivityHistory() { POID = result, PONumber = poObj.PONo.ToString() });
            }

            #endregion

            base.operationSuccess = success;//Set opeaon success
            _Session.ResetPOInSessionAndEmptyTempUpload(poObj.POGUID); // reset because going back to Manage will automatically creat new session
            
            if(success)
                TempData["printPOAfterSave"] = printPOAfterSave.HasValue && printPOAfterSave.Value;
            
            return RedirectToAction("Manage", new { POID = result });
        }

        [AccessPO("POID")]
        [CacheControl(HttpCacheability.NoCache), HttpGet]
        public ActionResult Archived(int POID)
        {
            POInternalPrint printView = new POInternalPrint();

            List<POComment> comments = new List<POComment>();
            List<POFile> filesH = new List<POFile>();
            List<PODetail> items = new List<PODetail>();

            #region Fetch PO data and set Viewstate
            vw_PO_Master_User_Loc vw = new POService().GetPOByIdForPrint(POID,
                ref comments, ref filesH, ref items, !_Session.IsOnlyCustomer);
            
            vw.POGUID = System.Guid.NewGuid().ToString();

            //Set data in View
            ViewData["comments"] = comments;
            ViewData["filesH"] = filesH;
            ViewData["items"] = items;

            printView.view = vw;
            printView.comments = comments;
            printView.filesH = filesH;
            printView.items = items;
            #endregion

            if (vw.ID == Defaults.Integer && vw.StatusID == Defaults.Integer && vw.AssignedTo == Defaults.Integer)
            { ViewData["Message"] = "PO not found"; return View("DataNotFound"); /* deleted po accessed from Log*/}
                        
            //Reset the Session PO object
            //PO poObj = POService.GetPOObjFromVW(vw);
            
            if (vw == null || vw.ID < 1)//Empty so invalid POID - go to Home
                return RedirectToAction("List", "Dashboard");

            return View(printView);
        }

        #endregion        
                
        
        #region Extra Functions (for PO actions)
        public POKOModel doAddEditPopulateKO(vw_PO_Master_User_Loc poData)
        {
            POKOModel vm = new POKOModel()
            {
                CVM = poData
                //POModel = new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(poData)
            };
            //ViewData["IsEditMode"] = (id != Defaults.Integer);
            vm.CVM.AssignedToOld = vm.CVM.AssignedTo;

            vm.Statuses = new LookupService().GetLookup(LookupService.Source.Status);
            vm.Brands = !_Session.IsOnlyVendor?new LookupService().GetLookup(LookupService.Source.BrandItems):
                //Special case for Vendor users (they must see only their Brands)
                new LookupService().GetLookup(LookupService.Source.BrandVendorItems,extras:_SessionUsr.OrgID.ToString());

            return vm;
        }
        #endregion
    }
}
namespace POT.DAL
{
    public class POKOModel
    {
        public vw_PO_Master_User_Loc CVM { get; set; }
        public IEnumerable Statuses { get; set; }
        public IEnumerable Brands { get; set; }
    }
}