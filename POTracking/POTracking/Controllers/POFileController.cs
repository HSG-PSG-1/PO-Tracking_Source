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
        bool IsAsync
        {
            get { return true; }
            /* until we upgrade code in future for Sync mode where changes will take place on the go instead of waiting until final commit */
            set { ;}
        }

        FileIO.mode HeaderFM { get { return (IsAsync ? FileIO.mode.asyncHeader : FileIO.mode.header); } }
        
        #region File Header Actions

        //Files List (PO\1\Files) & Edit (PO\1\Files\2)
        [CacheControl(HttpCacheability.NoCache), HttpGet]
        public ActionResult Files(int POID, string POGUID) // PartialViewResultViewResultBase 
        {
            ViewData["Archived"] = false;
            ViewData["POGUID"] = POGUID;
            return View();
        }

        [CacheControl(HttpCacheability.NoCache), HttpGet]
        public JsonResult FilesKOVM(int POID, string POGUID, int? FileID)
        {
            //Set File object
            POFile newObj = new POFile() { ID = -1, _Added = true, POID = POID, POGUID = POGUID,
                UploadedBy = _SessionUsr.UserName, LastModifiedBy = _SessionUsr.ID, LastModifiedDate = DateTime.Now, UploadDate = DateTime.Now,
                UserID = _SessionUsr.ID, FileName="", FileNameNEW="" };

            List<POFile> files = new List<POFile>();
            FileKOModel vm = new FileKOModel()
            {
                FileToAdd = newObj, EmptyFileHeader = newObj,
                AllFiles = (new POFileService().Search(POID, null))
            };
            // Lookup data
            vm.FileTypes = new LookupService().GetLookup(LookupService.Source.POFileType);

            return Json(vm, JsonRequestBehavior.AllowGet);
        }
                
        [HttpPost]
        [OutputCache(NoStore = true, Duration = 0, VaryByParam = "*")] //SO: 2570051/error-returning-ajax-in-ie7
        public ActionResult FilePostKO(int POID, string POGUID, POFile FileHdrObj)
        { 
            HttpPostedFileBase hpFile = Request.Files["FileNameNEW"];
            bool success = true;
            string result = "";// "Uploaded " + hpFile.FileName + "(" + hpFile.ContentLength + ")";

            #region New file upload

            if ((FileHdrObj.FileNameNEW ?? FileHdrObj.FileName) != null)
            {//HT Delete old\existing file? For Async need to wait until final commit
                //HT:IMP: Set Async so that now the file maps to Async file-path
                FileHdrObj.IsAsync = true;
                //FileHdrObj.POGUID = _Session.PO.POGUID; // to be used further
                #region Old code (make sure the function 'ChkAndSavePOFile' does all of it)
                //string docName = string.Empty;
                //FileIO.result uploadResult = SavePOFile(Request.Files["FileNameNEW"], ref docName, POID, true);

                //if (uploadResult != FileIO.result.successful)
                //    if (uploadResult == FileIO.result.duplicate)
                //        ModelState.AddModelError("FileName", "Duplicate file found");
                //    else
                //        ModelState.AddModelError("FileName", "Unable to upload file");
                #endregion
                FileHdrObj.FileName = ChkAndSavePOFile("FileNameNEW", POID, HeaderFM, FileHdrObj.POGUID);
                success = (ModelState["FileName"].Errors.Count() < 1);
            }

            #endregion
            result = !success ? ("Unable to upload file - " + ModelState["FileName"].Errors[0].ErrorMessage) : "";

            //Taconite XML
            return this.Content(Defaults.getTaconiteResult(success,
                Defaults.getOprResult(success, result), "fileOprMsg",
                "fileUploadResponse('" + FileHdrObj.CodeStr + "'," + success.ToString().ToLower() + "," + FileHdrObj.ID + ")"), "text/xml");
        }

        [SkipModelValidation]
        [AccessPO("POID")]
        [HttpPost]
        public ActionResult FileKODelete(int POID, string POGUID,[FromJson] POFile delFH)
        {//Call this ONLY when you need to actually delete the file
            bool proceed = false;
            if (delFH != null)
            {
                #region Delete File

                //If its Async - we can delete the TEMP file, if its sync the file is not present in TEMP folder so delete is not effective
                // HT: infer: send async because the file resides in the temp folder
                if (FileIO.DeletePOFile(delFH.FileName, POGUID, null, FileIO.mode.asyncHeader))
                {
                    //HT: INFER: Delete file for Async, Sync and (existing for Async - 
                    //the above delete will cause no effect coz path is diff)
                    //new CAWFile(IsAsync).Delete(new FileHeader() { ID = FileHeaderID, POGUID = POGUID });
                    proceed = true;
                }
                else
                    proceed = false;

                #endregion
            }
            //Taconite XML
            return this.Content(Defaults.getTaconite(proceed,
                Defaults.getOprResult(proceed, "Unable to delete file"), "fileOprMsg"), "text/xml");
        }

        #endregion

        #region Extra Actions and functions to get code for file download

        /// <summary>
        /// Check and Save PO File being uploaded. Set error in ModelState if any issue
        /// </summary>
        /// <param name="hpFileKey">HttpPost file browser control Id</param>
        /// <param name="POId">PO Id</param>
        /// <param name="PODetailId">PO Detail Id</param>
        /// <param name="upMode">FileIO.mode (Async or Sync & Header  or Detail)</param>
        /// <returns>File upload name</returns>
        string ChkAndSavePOFile(string hpFileKey, int POId, FileIO.mode upMode, string POGUID, int? PODetailId = null)
        {
            HttpPostedFileBase hpFile = Request.Files[hpFileKey];

            string docName = string.Empty;
            FileIO.result uploadResult = FileIO.UploadAndSave(hpFile, ref docName, POGUID, PODetailId, upMode);

            #region Add error in case of an Upload issue

            switch (uploadResult)
            {
                case FileIO.result.duplicate:
                    ModelState.AddModelError("FileName", "Duplicate file found"); break;
                case FileIO.result.noextension:
                    ModelState.AddModelError("FileName", "File must have an extension"); break;
                case FileIO.result.contentLength:
                    ModelState.AddModelError("FileName", string.Format("File size cannot exceed {0}MB", Config.MaxFileSizMB)); break;
                case FileIO.result.successful: break;
                default://Any other issue
                    ModelState.AddModelError("FileName", "Unable to upload file"); break;
            }

            #endregion

            return docName;
        }

        //Get Header File
        [ValidateInput(false)] // SO: 2673850/validaterequest-false-doesnt-work-in-asp-net-4
        public ActionResult GetFile()
        {
            try
            {
                string code = "";
                try { code = Request.QueryString.ToString(); }
                catch (HttpRequestValidationException httpEx)
                { code = Request.RawUrl.Split(new char[] { '?' })[1]; }//SPECIAL CASE for some odd codes!

                string[] data = DecodeQSforFile(code);
                string filename = data[0];

                #region SPECIAL CASE for Async uploaded file
                if (string.IsNullOrEmpty(filename))
                { // Can't use HttpUtility.UrlDecode in CodeStr property 
                    //- because it'll create issues with string.format and js function calls so handle in GetFile
                    data = DecodeQSforFile(HttpUtility.UrlDecode(code));
                    filename = data[0];
                }
                #endregion
                string POId = data[1];
                bool Async = bool.Parse(data[2]);//This must parse correctly
                //Send file stream for download
                return SendFile(POId, null, (Async ? FileIO.mode.asyncHeader : FileIO.mode.header), filename);
            }
            catch (Exception ex) { ViewData["Message"] = "File not found"; return View("DataNotFound"); }
        }
        // Send file stream for download
        private ActionResult SendFile(string POGUID, int? poDetailId, FileIO.mode fMode, string filename)
        {
            try
            {
                string filePath = FileIO.GetPOFilePath(POGUID, fMode, filename, true);

                if (System.IO.File.Exists(AppDomain.CurrentDomain.BaseDirectory + filePath))
                    /*System.IO.Path.GetFileName(filePath)*/
                    return File("~/" + filePath, "Content-Disposition: attachment;", filename);
                else/*Invalid or deleted file (from Log)*/
                { ViewData["Message"] = "File not found"; return View("DataNotFound"); }

            }
            catch (Exception ex) { return View(); }
        }

        /// <summary>
        /// Decode querystring for file download link
        /// </summary>
        /// <param name="code">string to be decoded</param>
        /// <returns>array of string</returns>
        private string[] DecodeQSforFile(string code)
        {
            if (string.IsNullOrEmpty(code)) return new string[] { };

            //code = HttpUtility.UrlDecode(HttpUtility.UrlDecode(code)); // decode URL (first is done by us and second by browser
            code = HttpUtility.UrlDecode(code); // Decoding twice creates issue for certain codes
            return Crypto.EncodeStr(code, false).Split(new char[] { POFile.sep[0] });
        }

        #endregion
    }
}

namespace POT.DAL
{
    public class FileKOModel
    {
        public POFile EmptyFileHeader { get; set; }
        public POFile FileToAdd { get; set; }
        public List<POFile> AllFiles { get; set; }
        public IEnumerable FileTypes { get; set; }
    }
}