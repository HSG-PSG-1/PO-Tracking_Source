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
{ // http://knockoutmvc.com/Home/QuickStart

    //[CompressFilter] - don't use it here
    //[IsAuthorize(IsAuthorizeAttribute.Rights.NONE)]//Special case for some dirty session-abandoned pages and hacks
    public partial class POController : BaseController
    {       
        //Comments List (PO\1\Comments) & Edit (PO\1\Comments\2)
        [CacheControl(HttpCacheability.NoCache), HttpGet]
        public ActionResult Comments(int POID, string POGUID, int AssignTo) // PartialViewResultViewResultBase 
        {
            ViewData["POGUID"] = POGUID;
            ViewData["AssignTo"] = AssignTo;
            return View();            
        }

        [CacheControl(HttpCacheability.NoCache), HttpGet]
        public JsonResult CommentsKOVM(int POID, string POGUID, int AssignTo) // PartialViewResultViewResultBase 
        {
            //if (_Session.IsOnlyCustomer) 
            //   return Json(null);//Customer doesn't have access to Comments

            //Set Comment object
            POComment newObj = new POComment() { ID = -1, _Added = true, POID = POID, POGUID = POGUID, CommentBy = _SessionUsr.UserName, LastModifiedBy = _SessionUsr.ID, LastModifiedDate = DateTime.Now, PostedOn = DateTime.Now, UserID = _SessionUsr.ID };

            CommentKOModel vm = new CommentKOModel()
            {
                CommentToAdd = newObj, EmptyComment = newObj, 
                //AllComments = (sendResult? comments : new CAWcomment(false).Search(POID, null, POGUID)),
                AllComments = new CommentService().Search(POID, null),//(new CAWcomment(false).Search(POID, null, POGUID)),
                AssignTo = AssignTo
            };

            vm.Users = new LookupService().GetLookup(LookupService.Source.User);

            return Json(vm, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        //HT: Kept for testing
        public ActionResult Comments(int? POID, [FromJson] IEnumerable<POComment> comments) // IEnumerable
        {
            #region Process based on ModelState (Old kept for review / ref)
            /*if (ModelState.IsValid)
            {
                bool changeAssignTo = (Request.Form["AssignTo"] != Request.Form["AssignToOLD"]);
                // Add new comment and also send flag to indicate if AssignTo was changed
                new CAWcomment(IsAsync).AddEdit(CommentObj, changeAssignTo,
                    int.Parse(Request.Form["AssignTo"]), Request.Form["AssignToVal"]);
                //Don, return to default action
                return RedirectToAction("Comments", new { POGUID = CommentObj.POGUID });
            }
            else
            {
                //http://stackoverflow.com/questions/279665/how-can-i-maintain-modelstate-with-redirecttoaction
                //TempData["ViewData"] = ViewData;//Store in temp intermediate variable
                TempData["PRGModel"] = new POT.Models.PRGModel().SetPRGModel<Comment>(CommentObj, ModelState);
                return RedirectToAction("Comments", new { POGUID = CommentObj.POGUID });
            }*/
            #endregion

            List<POComment> commentList = comments.ToList();
            
            commentList.Add(new POComment()
            { Comment1 = "I came from postback refresh! (to confirm a successful postback)", CommentBy = "Server postback" });

            Session["Comments_Demo"] = commentList;

            return View();// RedirectToAction("Comments");//new CommentKOModel()
        }

        [HttpPost]
        public JsonResult CommentsKOEmail(int POID, string POGUID, int AssignTo, string PONumber, [FromJson] POComment CommentObj)        
        {            
            bool sendMail = (POID > Defaults.Integer && AssignTo != _SessionUsr.ID);// No need to send mail if its current user
            try
            {
                #region Check and send email
                if (sendMail)
                {// No need to send mail if its current user
                    string UserEmail = new UserService().GetUserEmailByID(AssignTo);
                    MailManager.AssignToMail(PONumber, CommentObj.Comment1, POID, UserEmail, (_SessionUsr.UserName), true);
                }
                #endregion
            }
            catch (Exception ex) { sendMail = false; }
            return Json(sendMail, JsonRequestBehavior.AllowGet); ;// RedirectToAction("Comments");//new CommentKOModel()
        }        
    }
}

namespace POT.DAL
{
    public class CommentKOModel
    {
        public POComment EmptyComment { get; set; }
        public POComment CommentToAdd { get; set; }
        public List<POComment> AllComments { get; set; }
        public IEnumerable Users { get; set; }
        public int? AssignTo { get; set; }
    }
}