using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using POT.DAL;
using POT.Services;
using HSG.Helper;

namespace CPM.Controllers
{
    [IsAuthorize(IsAuthorizeAttribute.Rights.ManageUser)]
    public partial class UserController : BaseController
    {
        
        public UserController(): //HT: Make sure this is initialized with default constructor values!
            base(Config.UserListPageSize, UserService.sortOn, Filters.list.User){;}

        #region List Grid

        public ActionResult List()
        {
            ViewData["oprSuccess"] = base.operationSuccess;//oprSuccess will be reset after this
            searchOpts = _Session.Search[Filters.list.User];
            //Populate ddl Viewdata
            populateData(true);
            ViewData["gridPageSize"] = gridPageSize; // Required to adjust pagesize for grid
            return View(); // No need to return view - it'll fetched by ajax in partial rendering
        }

        #region Will need GET (for AJAX) & Post

        [CacheControl(HttpCacheability.NoCache)]
        public JsonResult UserList()
        {
            //Make sure searchOpts is assigned to set ViewState
            vw_Users_Role_Org oldSearchOpts = (vw_Users_Role_Org)searchOpts;
            searchOpts = new vw_Users_Role_Org();
            populateData(false);

            var result = from vw_u in new UserService().SearchKO((vw_Users_Role_Org)searchOpts)                             
                         select new
                         {
                             ID = vw_u.ID,
                             Email = vw_u.Email,
                             OrgID = vw_u.OrgID,
                             OrgName = vw_u.OrgName,
                             OrgType = vw_u.OrgType,
                             OrgTypeId = vw_u.OrgTypeId,
                             RoleID = vw_u.RoleID,
                             RoleName = vw_u.RoleName,
                             UserName = vw_u.UserName
                         };
            
            return Json(new { records = result, search = oldSearchOpts }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [SkipModelValidation]//HT: Use with CAUTION only meant for POSTBACK search Action        
        public JsonResult UserList(vw_Users_Role_Org searchObj, string doReset)
        {
            searchOpts = (doReset == "on") ? new vw_Users_Role_Org() : searchObj; // Set or Reset Search-options
            populateData(false);// Populate ddl Viewdata

            return Json(true);// We just need to set it in the session
        }

        [HttpPost]
        [SkipModelValidation]
        public ActionResult SetSearchOpts(vw_PO_Dashboard searchObj)
        {
            if (searchObj != null)
            {//Called only to set filter via ajax
                searchOpts = searchObj;
                return Json(true);
            }
            return Json(false);
        }

        #endregion

        [HttpPost]
        public ActionResult UserKODelete(int? UserId)
        {
            Users uObj = new Users() { ID = UserId.Value };
            bool proceed = false; string err = "";
            proceed = !(new UserService().IsReferred(uObj));//If user being deleted is referred abort            
            if (!proceed)
                err = POT.Models.Master.delRefChkMsg;
            else
            {
                proceed = !(uObj.ID == _SessionUsr.ID); // Self delete
                if (!proceed) err = "Cannot delete your own record!";
            }

            if (proceed) // NOT deleted because testing
            {//Delete & Log Activity
                new UserService().Delete(uObj);
                new ActivityLogService(ActivityLogService.Activity.UserDelete).Add();
            }
            //base.operationSuccess = proceed; HT: DON'T
            return this.Content(Defaults.getTaconite(proceed,
                Defaults.getOprResult(proceed, err), null, true), "text/xml");
        }

        #endregion

        #region Add, Edit & Delete

        public ActionResult AddEdit(int id)
        {
            Users usr = new UserService().GetUserById(id, _SessionUsr.OrgID);

            if (id > Defaults.Integer && usr.ID == Defaults.Integer && usr.OrgID == Defaults.Integer)
            { ViewData["Message"] = "User not found"; return View("DataNotFound"); /* deleted po accessed from Log*/}

            doAddEditPopulate(id, usr);

            return View(usr);
        }

        [HttpPost]
        public ActionResult AddEdit(int id, Users usr)//, string LinkedLoc, string UnlinkedLoc)
        {
            if (base.IsAutoPostback() || !ModelState.IsValid)
            {
                doAddEditPopulate(id, usr);
                //In case there's an invalid postback
                return View(usr);//Request.Form["chkDone"] must be present
            }
            int result = new UserService().AddEdit(usr);
            //Log Activity
            new ActivityLogService((id > Defaults.Integer) ? ActivityLogService.Activity.UserEdit : 
                ActivityLogService.Activity.UserAdd).Add();

            TempData["oprSuccess"] = true;
            return RedirectToAction("List");
        }
        
        [HttpPost]
        public ActionResult DeleteTaco(int? UserId)
        {
            Users uObj = new Users() { ID = UserId.Value };
            bool proceed = false; string err = "";
            proceed = !(new UserService().IsReferred(uObj));//If user being deleted is referred abort
            if (!proceed) 
                err = POT.Models.Master.delRefChkMsg;
            else
            {
                proceed = !(uObj.ID == _SessionUsr.ID); // Self delete
                if (!proceed) err = "Cannot delete your own record!";
            }
            
            if (proceed)
            {//Delete & Log Activity
                new UserService().Delete(uObj);
                new ActivityLogService(ActivityLogService.Activity.UserDelete).Add();
            }
            //base.operationSuccess = proceed; HT: DON'T
            return this.Content(Defaults.getTaconite(proceed,
                Defaults.getOprResult(proceed, err), null, true), "text/xml");
        }

        #endregion

        #region Extra Functions

        public void doAddEditPopulate(int id, Users usr)
        {
            ViewData["IsEditMode"] = (id > Defaults.Integer);
            populateData(true);
        }

        public void populateData(bool fetchOtherData)//object of type: vw_Users_Role_Org
        {
            //Set any other constraint on searchObj
            if (fetchOtherData)
            {
                //ViewData["Orgs"] = new OrgService().GetOrgs(); - useful in case ORgs filter is needed
                ViewData["Roles"] = new SecurityService().GetRolesCached();
            }
        }

        #endregion
    }
}
