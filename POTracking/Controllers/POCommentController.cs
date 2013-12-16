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
        [HttpPost]
        public JsonResult CommentsKOEmail(int POID, /*string POGUID,*/ int AssignTo, string PONumber, [FromJson] POComment CommentObj)        
        {
            bool sendMail = CommentService.SendEmail(POID, AssignTo, PONumber, CommentObj);
            return Json(sendMail, JsonRequestBehavior.AllowGet); ;// RedirectToAction("Comments");//new CommentKOModel()
        }        
    }
}

namespace POT.DAL
{
    public class CommentVM
    {
        public POComment EmptyComment { get; set; }
        public POComment CommentToAdd { get; set; }
        public List<POComment> AllComments { get; set; }
        public IEnumerable Users { get; set; }
        public int? AssignTo { get; set; }
    }
}