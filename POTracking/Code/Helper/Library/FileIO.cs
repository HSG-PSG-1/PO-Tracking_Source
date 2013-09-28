using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI.WebControls;
using System.IO;

namespace HSG.Helper
{
    public class FileIO
    {
        #region Variables
        public const string sep = ";";

        public static readonly char dirPathSep = System.IO.Path.DirectorySeparatorChar;
        
        public const char webPathSep = '/';
        
        public enum result
        { 
            successful,
            emptyNoFile,
            fileUploadIssue,
            contentLength,
            duplicate,
            noextension
        }

        public enum mode
        {
            header,
           // detail,
            asyncHeader
           // asyncDetail
        }

        static string GetHD(mode upMode)
        { //HT:FileH: NOT useful until we have two diff file uploads (like Header & Detail)
            switch (upMode)
            {
                //case mode.detail: return "D";
                case mode.header: return "H";
                case mode.asyncHeader: return "H_Temp";
                //case mode.asyncDetail: return "D_Temp";
                default: return "H";//non-reachable
            }
        }

        #endregion

        #region Upload
        
        public static result UploadAndSave(HttpPostedFileBase upFile, ref string docName, string POGUID, int? DetailId, mode upMode)
        {
            #region Init variables
            
            string subsubDir = GetHD(upMode); 
            bool hasDetail = (DetailId != null);
            result resultIO = result.emptyNoFile;

            string ext = Path.GetExtension(upFile.FileName);//Get extension
            docName = Path.GetFileNameWithoutExtension(upFile.FileName);//Get only file name

            #endregion

            #region Issue with file name/ path / extension
            
            if (upFile == null || string.IsNullOrEmpty(upFile.FileName) || upFile.ContentLength < 1)
                return resultIO;
            else if(string.IsNullOrEmpty(ext))
                //Security (review in future)http://www.dreamincode.net/code/snippet1796.htm
                //if (ext.ToLower() == "exe" || ext.ToLower() == "ddl")
                return result.noextension;
            
            #endregion

            try
            {
                if (upFile.ContentLength > Config.MaxFileSizMB*1024*1024)
                    return result.contentLength;
                else
                {
                    //Get full path
                    string fullPath = CheckOrCreateDirectory(Config.UploadPath, POGUID, subsubDir);
                    //Special case for Details dir (check & create a poID/D/detailID directory)
                    if (hasDetail) fullPath = CheckOrCreateDirectory(fullPath, DetailId.ToString());
                    // Gen doc name
                    docName = docName + ext;
                    // Check file duplication
                    if (//upMode != mode.asyncDetail && upMode != mode.asyncHeader &&
                        File.Exists(Path.Combine(fullPath, docName)))
                        return result.duplicate;//Duplicate file exists!

                    // All OK - so finally upload
                    upFile.SaveAs(Path.Combine(fullPath, docName));//Save or Overwrite the file
                }
            }
            catch { return result.fileUploadIssue; }

            return result.successful;
        }

        #endregion

        #region Check / Create / Delete Directory & File

        public static string CheckOrCreateDirectory(string uploadPath, string dir, string subDir)
        {//i.e. ../../Files/2/H
            uploadPath = CheckOrCreateDirectory(uploadPath, dir);//Check and create directory
            return CheckOrCreateDirectory(uploadPath, subDir);//Check and create SUB directory
        }

        public static string CheckOrCreateDirectory(string uploadPath, string dirName)
        {
            if (!Directory.Exists(Path.Combine(uploadPath, dirName)))//Check and create directory
                Directory.CreateDirectory(Path.Combine(uploadPath, dirName));
            
            return Path.Combine(uploadPath, dirName);
        }
                
        public static void EmptyDirectory(string delPath)
        {
            if (!Directory.Exists(delPath) || delPath == Config.UploadPath)
                return; // avoid worst cases
            try
            {
                Directory.Delete(delPath, true);
            }
            catch (System.IO.IOException ex)
            {
                //Or refer the following to set system attributes when delete
                //http://stackoverflow.com/questions/611921/how-do-i-delete-a-directory-with-read-only-files-in-c
            }
        }

        public static bool DeleteFile(string FileName)
        {
            try
            {
                string FilePath = Path.Combine(Config.UploadPath, FileName);

                if (File.Exists(FilePath))
                    File.Delete(FilePath);
                
                return true; // HT: If file doesn't exist - we need not worry to delete it!
                
            }
            catch { return false; }

            //return false;
        }

        #endregion

        #region PO File specific functions

        public static string GetPOFilePath(string POGUID, mode upMode, string FileName, bool webURL)
        {
            if (string.IsNullOrEmpty(POGUID) || (string.IsNullOrEmpty(FileName) && webURL))
                return "#"; 

            string basePath = webURL ? Config.DownloadUrl : Config.UploadPath;
            char sep = (webURL ? webPathSep : dirPathSep);
            //HT:FileH: string dirPath = basePath + sep + POGUID + sep + GetHD(upMode); // might be web url or physical path so can't use Path.Combine
            string dirPath = basePath + sep + POGUID;
            //HT:FileH: SPECIAL case ONLY for POT (filedirectly in the POID folder)
            if (upMode == mode.asyncHeader)
                dirPath = dirPath + sep + GetHD(upMode);

            //if (PODetailID != null) //Special case for Details file
            //    dirPath = dirPath + sep + PODetailID.Value.ToString();

            return string.IsNullOrEmpty(FileName) ? dirPath : dirPath + sep + FileName;
        }

        public static string Merge(string uri1, string uri2)
        {
            uri1 = uri1.TrimEnd('/');
            uri2 = uri2.TrimStart('/');
            return string.Format("{0}/{1}", uri1, uri2);
        }

        public static string GetPODirPathForDelete(int POID, int? PODetailID, string POGUID, bool IsAsync)
        {//Called from PO-delete or PODetail delete
           string poPath = Path.Combine(Config.UploadPath, (IsAsync?POGUID.ToString():POID.ToString()));
           if (PODetailID == null)
               return poPath; // returned to PO - delete
           else //if (DetailID != null) 
               return poPath; //return Path.Combine(Path.Combine(poPath, GetHD(IsAsync ? mode.asyncDetail : mode.detail)), PODetailID.Value.ToString()); //// returned to PODetail (Item) - delete
        }

        public static string GetPOFilesTempFolder(string POGUID, bool IsHeader)
        {//Called from PO-delete or PODetail delete
            string poPath = Path.Combine(Config.UploadPath, POGUID.ToString());
            //HT:FileH: return Path.Combine(poPath, GetHD(mode.asyncHeader));//IsHeader ? mode.asyncHeader : mode.asyncDetail));
            return Path.Combine(poPath, GetHD(mode.asyncHeader));
        }

        public static bool DeletePOFile(string docName, int POID, int? PODetailId, FileIO.mode upMode)
        { return DeletePOFile(docName, POID.ToString(), PODetailId, upMode); }
        public static bool DeletePOFile(string docName, string POGUID, int? PODetailId, FileIO.mode upMode)
        {
            return FileIO.DeleteFile(GetPOFilePath(POGUID, upMode, docName, false));            
        }
        
        #endregion

        #region Move / Get File download code

        public static void MoveAsyncPOFiles(int poID,string POGUID)
        {// Move all Async uploaded files from H_Temp to H

            mode FMode = mode.header;// (isHeader ? mode.header : mode.detail);
            mode aFMode = mode.asyncHeader;// (isHeader ? mode.asyncHeader : mode.asyncDetail);

            string sourcePath = GetPOFilePath(POGUID, aFMode, "", false);
            string targetPath = GetPOFilePath(poID.ToString(), FMode, "", false);

            if (!Directory.Exists(sourcePath))
                return;//Means there were only delete records which are already deleted

            //check if the target directory exists (special case for first time upload during Async mode)
            if (!Directory.Exists(targetPath))   
                Directory.CreateDirectory(targetPath);

            DirectoryInfo di = new DirectoryInfo(sourcePath);
            //MOVE all the files into the new directory
            foreach (FileInfo fi in di.GetFiles())
                fi.CopyTo(Path.Combine(targetPath, fi.Name), true);
            
            // !! HT - handled after PO entry save 
            //Finally empty the source temp DIR
            //EmptyDirectory(sourcePath);
        }

        public static string getFileDownloadCode(string FileName, string POGUID)
        {
            string codeStr = FileName + sep + POGUID + sep + (false).ToString();
            codeStr = HttpUtility.UrlEncode(Crypto.EncodeStr(codeStr.ToString(), true));
            // Make sure you do UrlEncode TWICE in code to get the code!!!
            return codeStr;
        }

        public static string getFileDownloadActionCode(string FileName, int POID, int? PODetailID)
        {
            bool isDetailFile = PODetailID.HasValue;
            
            System.Text.StringBuilder codeStr = new System.Text.StringBuilder(FileName + sep + POID.ToString() + sep);
            if (isDetailFile) codeStr.Append(PODetailID.ToString() + sep);
            codeStr.Append((false).ToString());

            // Make sure you do UrlEncode TWICE in code to get the code!!!
            return (isDetailFile?"GetFileD?":"GetFile?") + HttpUtility.UrlEncode(Crypto.EncodeStr(codeStr.ToString(), true));
        }

        #endregion

        //Merge two directories
        //http://stackoverflow.com/questions/9053564/c-sharp-merge-one-directory-with-another
    }
}
