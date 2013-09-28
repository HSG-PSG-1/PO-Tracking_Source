using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using System.Text.RegularExpressions;
using POT.Services;
using HSG.Helper;

namespace POT.DAL
{
    [Serializable]
    public abstract class Opr
    {
        #region Variables & Properties

        public const string sep = ";";

        public bool _Added { get; set; }
        public bool _Edited { get; set; }
        public bool _Deleted { get; set; }
        // common property required for all the PO and its child objects
        public string POGUID { get; set; }

        #endregion

        // Set some required fields to proceed (mostly overridded in child class)
        public void setOpr(int ID)
        {// Default settings
            this._Added = (ID <= Defaults.Integer) && !(this._Deleted);
            this._Edited = (!this._Added) && !(this._Deleted);
        }
    }

    #region PO Model (& vw_PO_Master_User_Loc)

    [Serializable, System.Xml.Serialization.XmlRoot(ElementName = "PO", IsNullable = true)]
    public partial class PO : POHeader
    {
        #region Extra Variables & Properties

        public List<POComment> aComments { get; set; }
        public List<PODetail> aItems { get; set; }
        public List<POFile> aFiles { get; set; }

        public bool AssignToChanged { get; set; }
        public string AssignToVal { get; set; }
        public string AssignToComment { get; set; }

        public string POGUID { get; set; } // common property required for all the PO and its child objects

        #endregion
    }

    [MetadataType(typeof(vw_PO_Master_User_LocMetadata))]
    public partial class vw_PO_Master_User_Loc
    {
        #region Extra Variables & Properties

        public int AssignedToOld { get; set; }
        public int StatusIDold { get; set; }
        public string POGUID { get; set; }
        //public string LocationAndCode { get { return Common.getLocationAndCode(this.Location, this.LocationCode); } }

        #endregion
    }

    public class vw_PO_Master_User_LocMetadata
    {
        [DisplayName("PO #")]
        [Required(ErrorMessage = "PO #" + Defaults.RequiredMsgAppend)]
        public int PONumber { get; set; }

        [DisplayName("Status")]
        [Required(ErrorMessage = "Status" + Defaults.RequiredMsgAppend)]
        public int OrderStatusID { get; set; }

        [DisplayName("Customer")]
        [Required(ErrorMessage = "Customer" + Defaults.RequiredMsgAppend)]
        public int CustID { get; set; }

        [DisplayName("Brand")]
        [Required(ErrorMessage = "Brand" + Defaults.RequiredMsgAppend)]
        public int BrandID { get; set; }

        [DisplayName("PO Date")]
        [Required(ErrorMessage = "PO Date" + Defaults.RequiredMsgAppend)]
        //[Range(typeof(DateTime), System.Data.SqlTypes.SqlDateTime.MinValue.ToString(), System.Data.SqlTypes.SqlDateTime.MaxValue.ToString())]//SO: 1406046
        [Range(typeof(DateTime), "1-Jan-1753", "31-Dec-9999")]
        public int PODate { get; set; }

        [DisplayName("Vendor")]
        [StringLength(30, ErrorMessage = Defaults.MaxLengthMsg)]
        public int VendorName { get; set; }

        [DisplayName("Location")]
        [Required(ErrorMessage = "Location" + Defaults.RequiredMsgAppend)]
        public int ShipToLocationID { get; set; }
    }

    #endregion

    #region PODetail Model

    [Serializable]
    public partial class PODetail : Opr
    {
        #region Extra Variables & Properties

        public string ItemCode { get; set; }

        public string UnitCostStr { get { return (UnitCost.HasValue ? UnitCost.Value : 0.0m).ToString("#0.00"); } }
        public string OrderExtensionStr { get { return (OrderExtension.HasValue ? OrderExtension.Value : 0.0m).ToString("#0.00"); } }

        #endregion
    }

    #endregion

    #region Comment Model

    [MetadataType(typeof(CommentMetadata))]
    public partial class POComment : Opr
    {
        #region Extra Variables & Properties
        public string CommentBy { get; set; }
        #endregion

        /* Set some required fields to proceed
        public Comment setProp()
        {
            if (!_Deleted)
            {//set necessary fields for Add & Edit
                this.LastModifiedDate = DateTime.Now;
                this.CommentBy = _SessionUsr.UserName;
                this.UserID = _SessionUsr.ID;
                this.PostedOn = DateTime.Now;
            }

            return this;
        }

        /// <summary>
        /// Add, Edit or Delete
        /// </summary>
        /// <param name="aComments"> List of object</param>
        /// <returns>Updated list of objects</returns>
        public List<Comment> doOpr(List<Comment> aComments)
        {
            if (aComments == null) aComments = new List<Comment>();//When there're NO records

            int index = aComments.FindIndex(p => p.ID == this.ID);//SO: 361921/list-manipulation-in-c-using-linq
            base.setOpr(ID);//Set Add or Edit

            #region Set data as per Operation

            if (_Deleted)//Deleted =================
            {
                if (ID < 0) aComments.RemoveAt(index);//remove newly added
                else aComments[index]._Deleted = true; 
            }
            else if (_Edited)//Edited=================
            {
                aComments[index] = this;
            }
            else //Added(or Newly added is edited)================
            {
                #region (New record: we assign -ve POId to avoid conflicts)
                if (index < 0)
                {
                    ID = (aComments.Count > 0) ?  (aComments.Min(c => c.ID) - 1) : Defaults.Integer - 1;
                    while (ID >= 0) { ID = ID - 1; }//Make it < 0
                    aComments.Add(this);
                }
                #endregion
                else //Newly added is edited(we still maintain the flag until final commit)
                    aComments[index] = this;
            }

            #endregion

            return aComments;
        }
        /// <summary>
        /// Add items to list
        /// </summary>
        /// <param name="child">Session PO value</param>
        /// <param name="records">Items</param>
        /// <returns>Updated PO variable</returns>
        public static PO lstAsync(PO parent, List<Comment> records)
        {
            if (parent == null || records == null || records.Count < 1) return parent;

            parent.aComments.AddRange(records);

            return parent;
        }
        */
    }

    public class CommentMetadata
    {
        [DisplayName("Comment")]
        [Required(ErrorMessage = "Comment" + Defaults.RequiredMsgAppend)]
        // Based on : http://www.w3schools.com/SQl/sql_datatypes.asp
        [StringLength(4000, ErrorMessage = Defaults.MaxLengthMsg)]
        public string Comment1 { get; set; }

        [DisplayName("Comment By")]
        public string CommentBy { get; set; }
    }

    #endregion

    #region POFile Model

    [MetadataType(typeof(POFileMetadata))]
    public partial class POFile : Opr
    {
        #region Extra Variables & Properties

        public bool IsAsync { get; set; }
        string _POGUID;
        public string POGUID
        {
            get
            { // Return _POGUID only for Async entries
                return string.IsNullOrEmpty(_POGUID) ? POID.ToString() : _POGUID;
            }
            set { _POGUID = value; }
        }

        public string UploadedBy { get; set; }
        public string FileNameNEW { get; set; }
        public string FileTypeTitle { get; set; }
        public string CodeStr
        {
            get
            {
                if (_Added) return string.Empty;
                else
                    return HttpUtility.UrlEncode(HSG.Helper.Crypto.EncodeStr(FileName + sep + POGUID.ToString() + sep + IsAsync, true));
            }
        } // Can't use HttpUtility.UrlDecode - because it'll create issues with string.format and js function calls so handle in GetFile

        public string FilePath
        { //HT: Usage: <a href='<%= Url.Content("~/" + item.FilePath) %>' target="_blank">
            get
            {
                return FileIO.GetPOFilePath
                    (POGUID, null, (IsAsync ? FileIO.mode.asyncHeader : FileIO.mode.header), FileName, true);
            }
        }

        #endregion
    }

    public class POFileMetadata
    {
        [DisplayName("File")]
        /* HT: DON'T - we've handled it from within the controller along with a special case of Update
         [Required(ErrorMessage = "Select a file to be uploaded")] */
        public string FileName { get; set; }

        [DisplayName("Uploaded By")]
        public string UploadedBy { get; set; }

        [DisplayName("Type")]
        public string FileTypeTitle { get; set; }

        [Required(ErrorMessage = "File Type" + Defaults.RequiredMsgAppend)]
        public string FileType { get; set; }

        [StringLength(250, ErrorMessage = Defaults.MaxLengthMsg)]
        public string Comment { get; set; }
    }

    #endregion
}
