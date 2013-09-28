using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using POT.DAL;
using POT.Services;
using HSG.Helper;
//using StackExchange.Profiling;

namespace POT.Controllers
{
    //[CompressFilter] - DON'T
    [IsAuthorize(IsAuthorizeAttribute.Rights.NONE)]//Special case for some dirty session-abandoned pages and hacks
    public partial class DashboardController : BaseController
    {
        string view = _Session.IsOnlyVendor ? "ListVendor" : "ListInternal";

        public DashboardController() : //HT: Make sure this is initialized with default constructor values!
            base(Config.DashboardPageSize, DashboardService.sortOn, Filters.list.Dashboard) { ;}

        #region List Grid Excel
        //[CompressFilter] - DON'T
        public ActionResult List(int? index, string qData)
        {
            index = index ?? 0;
            //_Session.NewSort = DashboardService.sortOn1; _Session.OldSort = string.Empty;//Initialize (only once)
            //base.SetSearchOpts(index.Value);
            //Special case: Set the filter back if it existed so that if the user "re-visits" the page he gets the previous filter (unless reset or logged off)
            searchOpts = _Session.Search[Filters.list.Dashboard];//new vw_PO_Dashboard();

            populateData(true);
            ViewData["gridPageSize"] = gridPageSize; // Required to adjust pagesize for grid

            // No need to return view - it'll fetched by ajax in partial rendering
            return View();
        }

        #region Will need GET (for AJAX) & Post
        
        [CacheControl(HttpCacheability.NoCache)]//Don't mention GET or post as this is required for both!
        public JsonResult POListKO(int? index, string qData, bool? fetchAll)
        {
            base.SetTempDataSort(ref index);// Set TempDate, Sort & index
            //Make sure searchOpts is assigned to set ViewState
            vw_PO_Dashboard oldSearchOpts = (vw_PO_Dashboard)searchOpts;
            searchOpts = new vw_PO_Dashboard();
            populateData(false);

            index = (index > 0) ? index + 1 : index; // paging starts with 2

            var result = from vw_u in new DashboardService().SearchKO(
                sortExpr, index, gridPageSize * 2, (vw_PO_Dashboard)searchOpts, fetchAll ?? false, _Session.IsOnlyVendor)
                         select new
                         {
                             ID = vw_u.ID,
                             PONumber = vw_u.PONumber,
                             OrderStatusID = vw_u.OrderStatusID,
                             AssignTo = vw_u.AssignTo,
                             VendorID = vw_u.VendorID,
                             VendorName = vw_u.VendorName,
                             BrandName = vw_u.BrandName,
                             Status = vw_u.Status,
                             CommentsExist = vw_u.CommentsExist,
                             FilesHExist = vw_u.FilesHExist,
                             PODateOnly = vw_u.PODateOnly,
                             ETAOnly = vw_u.ETAOnly,
                             ETDOnly = vw_u.ETDOnly,
                             ShipToCity = vw_u.ShipToCity
                         };
             
            return Json(new { records = result, search = oldSearchOpts }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [SkipModelValidation]//HT: Use with CAUTION only meant for POSTBACK search Action
        public ActionResult POListKO(vw_PO_Dashboard searchObj, string doReset, string orderBy, bool? fetchAll)
        {
            searchOpts = (doReset == "on") ? new vw_PO_Dashboard() : searchObj; // Set or Reset Search-options
            populateData(false);// Populate ddl Viewdata

            //Ensure that Orderby has the correcy field (not the custom field so need to replace)
            orderBy = orderBy.Replace("PODateOnly", "PODate").Replace("ETDOnly", "ETD").Replace("ETAOnly", "ETA");

            _Session.POIDs = new DashboardService().SearchPOIDKO(searchObj, orderBy); 

            return Json(true);// WE just need to set it in the session
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
        [SkipModelValidation]
        public ActionResult Excel()
        {
            //HttpContext context = ControllerContext.HttpContext.CurrentHandler;
            //Essense of : http://stephenwalther.com/blog/archive/2008/06/16/asp-net-mvc-tip-2-create-a-custom-action-result-that-returns-microsoft-excel-documents.aspx
            this.Response.Clear();
            this.Response.AddHeader("content-disposition", "attachment;filename=" + "Dashboard_" + _SessionUsr.ID + ".xls");
            this.Response.Charset = "";
            this.Response.Cache.SetCacheability(HttpCacheability.NoCache);
            this.Response.ContentType = "application/vnd.ms-excel";

            //DON'T do the following
            //this.Response.Write(content);
            //this.Response.End();

            populateData(false);
            var result = new DashboardService().Search(sortExpr, 1, gridPageSize, (vw_PO_Dashboard)searchOpts, true, _Session.IsOnlyVendor);

            searchOpts = new vw_PO_Dashboard();
            populateData(false);

            return View("Excel", result);
        }

        /*public ActionResult ExcelPDF()
        {   
            populateData(false);
            List<vw_PO_Dashboard> printView = new DashboardService().Search(sortExpr, 1, gridPageSize, (vw_PO_Dashboard)searchOpts, true, _Session.IsOnlyVendor);
            
            string GUID = _SessionUsr.ID.ToString();
            return new ReportManagement.StandardPdfRenderer().BinaryPdfData(this, "Dashboard" + GUID, "Excel", printView);
        }*/
        #endregion

        #region Dialog Actions
        //[AccessPO("POID")]
        [CacheControl(HttpCacheability.NoCache), HttpGet]
        public ActionResult Comments(int POID)
        {
            return View(new CommentService().Search(POID, null));
        }

        //[AccessPO("POID")]
        [CacheControl(HttpCacheability.NoCache), HttpGet]
        public ActionResult Files(int POID)
        {
            return View(new POFileService().Search(POID, null));
        }
        
        [CacheControl(HttpCacheability.NoCache), HttpGet]
        public ActionResult Status(int POID)
        {//Redirect to PO\X\Status?Archived = true (ref: http://forums.asp.net/t/1202550.aspx/1)
            return RedirectToAction("Status", "PO", new { POID = POID });
            //?Archived=" + Archived.ToString()
        }
        #endregion

        #region Extra Functions

        public void populateData(bool fetchOtherData)
        {
            //using (MiniProfiler.Current.Step("Populate lookup Data"))
            {
                vw_PO_Dashboard searchOptions = (vw_PO_Dashboard)(searchOpts);
                if (_Session.IsOnlyVendor) searchOptions.VendorID = _SessionUsr.OrgID;//Set the Vendor filter

                if (fetchOtherData)
                {
                    ViewData["Status"] = new LookupService().GetLookup(LookupService.Source.Status);
                    ViewData["UserList"] = new LookupService().GetLookup(LookupService.Source.User);
                }
            }
        }

        #endregion
    }
}
