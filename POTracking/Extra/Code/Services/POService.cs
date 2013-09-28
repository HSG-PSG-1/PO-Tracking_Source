using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic;
using System.Data.Linq.SqlClient;
using POT.Models;
using POT.DAL;
using HSG.Helper;
using Webdiyer.WebControls.Mvc;

namespace POT.Services
{
    public class POService : _ServiceBase
    {
        #region Variables & Constructor
        
        public readonly vw_PO_Master_User_Loc emptyView = new vw_PO_Master_User_Loc() {
            ID = Defaults.Integer, PODate = DateTime.Now, CustID = _SessionUsr.OrgID, CustOrg = _SessionUsr.OrgName };
        public readonly PO emptyPO = new PO() { ID = Defaults.Integer, 
            aComments= new List<POComment>(), aFiles=new List<POFile>(), aItems  = new List<PODetail>() }; // Add empty files, comments and items to ensure not null handling

        public POService() : base() {;}
        public POService(POTmodel dbcExisting) : base(dbcExisting) {;}
        
        #endregion

        #region Search / Fetch

        public vw_PO_Master_User_Loc GetPOById(int id)
        {
            using (dbc)
            {
                vw_PO_Master_User_Loc vw_c = (from vw in dbc.vw_PO_Master_User_Locs where vw.ID == id
                                                 select vw).SingleOrDefault<vw_PO_Master_User_Loc>();                
                
                if (vw_c != null)
                    vw_c.StatusIDold = vw_c.StatusID;
                else
                    vw_c = emptyView;

                return vw_c;
            }
        }

        public vw_PO_Master_User_Loc GetPOByIdForPrint(int poId,ref List<POComment> comments,
            ref List<POFile> filesH,ref List<PODetail> items, bool loadComments)
        {
            using (dbc)
            {
                vw_PO_Master_User_Loc vw_c = (from vw in dbc.vw_PO_Master_User_Locs
                                                 where vw.ID == poId
                                                 select vw).SingleOrDefault<vw_PO_Master_User_Loc>();

                if (vw_c != null)
                    vw_c.StatusIDold = vw_c.StatusID;
                else
                    vw_c = emptyView;

                // Load comments
                if (loadComments) comments = new CommentService().Search(poId, null);//Only for non-customers
                // Load Files
                filesH = new FileHeaderService().Search(poId,null);
                //items = new PODetailService().Search(poId, null);

                return vw_c;
            }
        }
                
        public static PO GetPOObjFromVW(vw_PO_Master_User_Loc vw)
        {
            vw.POGUID = vw.POGUID??System.Guid.NewGuid().ToString();// MAke sure the GUID is set at this initial level
            return new PO()
            {ID = vw.ID,
                PODate = vw.PODate,
                PONo = vw.PONo, //HT: Needed for Comment-AssignTo email (Auto-Generated)
                CustID = vw.CustID,
                CustRefNo = vw.CustRefNo,
                SalespersonID = vw.SalespersonID,
                ShipToLocationID = vw.ShipToLocationID,
                StatusID = vw.StatusID,
                //VendorID = vw.VendorID,
                POGUID = vw.POGUID
            };
        }

        public static vw_PO_Master_User_Loc GetVWFromPOObj(PO c, string SPNameForCustomer)
        {
            return new vw_PO_Master_User_Loc()
            {
                ID = c.ID,
                AssignedTo = c.AssignedTo, //HT? : CAUTION: Handle default Assignee !!!
                //AssignedToVal = c.AssignedToVal,
                BrandID = c.BrandID,
                PODate = c.PODate,
                PONo = c.PONo, //HT: Needed for Comment-AssignTo email (Auto-Generated)
                CustID = c.CustID,
                CustRefNo = c.CustRefNo,
                SalespersonID = c.SalespersonID,
                SalespersonName = SPNameForCustomer, // HT: Need to default for customer
                ShipToLocationID = c.ShipToLocationID,
                StatusID = c.StatusID,
                //VendorID = c.VendorID,
                POGUID = c.POGUID // HT: Make sure this is set!
            };
        }

        #endregion

        #region Add / Edit / Delete / Archive / Add Default

        public int Add(PO poObj)
        {
            //PO poObj = GetPOObjFromVW(vwObj);
            //triple ensure that the latest comment.PostedOn date is NOT null
            poObj.PODate = DateTime.Now;
            //Set lastmodified fields
            poObj.LastModifiedBy = _SessionUsr.ID;
            poObj.LastModifiedDate = DateTime.Now;

            poObj.StatusHistories.Add(new StatusHistory()
            {
                PO = poObj, //POID = poObj.ID,
                LastModifiedBy = _SessionUsr.ID,
                LastModifiedDate = DateTime.Now,
                OldStatusID = Defaults.Integer,
                NewStatusID = poObj.StatusID
            });

            dbc.POHeaders.InsertOnSubmit(poObj);
            dbc.SubmitChanges();
            poObj.PONo = poObj.ID;

            return poObj.ID; // Return the 'newly inserted id'
        }

        public PO AddDefault(int userID, int OrgID, bool defaultOrgSP,ref string spNameForCustomer)
        {
            DefaultPO dDB = DefaultDBService.GetPO(userID);
            PO poObj = new PO()
            {
                AssignedTo = dDB.AssignTo,
                PODate = dDB.PODate,
                CustID = dDB.CustID,
                ShipToLocationID = dDB.ShipToLocID,
                StatusID = dDB.StatusID,
                POGUID = System.Guid.NewGuid().ToString() // MAke sure the GUID is set at this initial level
            };

            poObj.ID = Defaults.Integer;
            //Set lastmodified fields
            poObj.LastModifiedBy = _SessionUsr.ID;
            poObj.LastModifiedDate = DateTime.Now;

            #region Kept for future ref - also must be done in Add po
            /* poObj.StatusHistories.Add(new StatusHistory() { PO = poObj, //POID = poObj.ID,
                LastModifiedBy = _SessionUsr.ID, LastModifiedDate = DateTime.Now, 
                OldStatusID = Defaults.Integer, NewStatusID = poObj.StatusID });

            dbc.POHeaders.InsertOnSubmit(poObj);
            dbc.SubmitChanges(); */
            #endregion

            #region Special case for customer - pre populate SP
            if (defaultOrgSP)
            {
                try
                {
                    var spData = from i in dbc.vw_CustOrg_SalesUsers where i.ID == OrgID select i;
                    poObj.SalespersonID = spData.ToList()[0].SalespersonId.Value;
                    spNameForCustomer = spData.ToList()[0].UserName;
                }
                catch(Exception ex){}// handles emply SP
                //OrgID  
            }
            #endregion

            return poObj; // Return the 'newly configured po'
        }
        
        public int AddEdit(PO poObj, int StatusIDold, bool doSubmit)
        {
            if (poObj.ID <= Defaults.Integer) // Insert
                return Add(poObj);

            // Update

            //Set lastmodified fields
            poObj.LastModifiedBy = _SessionUsr.ID;
            poObj.LastModifiedDate = DateTime.Now;

            dbc.POHeaders.Attach(poObj);//attach the object as modified
            dbc.Refresh(System.Data.Linq.RefreshMode.KeepCurrentValues, poObj);//Optimistic-concurrency (simplest solution)

            #region If the Status has been changed then make entry in StatusHistory
            if (poObj.StatusID != StatusIDold)
                new StatusHistoryService(dbc).Add(new StatusHistory()
                {
                    POID = poObj.ID,
                    NewStatusID = poObj.StatusID,
                    OldStatusID = StatusIDold                    
                }, false);
            #endregion

            if (doSubmit)    dbc.SubmitChanges();
            // Set PO #
            poObj.PONo = poObj.ID;

            return poObj.ID;
        }

        public void Delete(PO poObj)
        {
            //HT: IMP: SP way of checking if an FK ref exists: http://stackoverflow.com/questions/5077423/sql-server-check-if-child-rows-exist
            dbc.POHeaders.DeleteOnSubmit(dbc.POHeaders.Single(c => c.ID == poObj.ID));
            //Delete PO Activities ???
            dbc.SubmitChanges();
        }

        #endregion

        #region Extra functions

        public bool AssignPO(int poId, int AssignTo)
        {
            if (poId <= Defaults.Integer || AssignTo == Defaults.Integer)
                return false;

            else
            {
                #region Update
                PO cObj = (from c in dbc.POHeaders where c.ID == poId select c).SingleOrDefault<PO>();

                if (cObj.ID <= Defaults.Integer) return false;

                cObj.AssignedTo = AssignTo;
                //Set lastmodified fields
                cObj.LastModifiedBy = _SessionUsr.ID;
                cObj.LastModifiedDate = DateTime.Now;

                //dbc.POHeaders.Attach(cObj);//attach the object as modified NOT needed as we just fetched it and dbc is ALIVE
                dbc.SubmitChanges();
                #endregion
            }

            return true;
        }

        internal bool IsPOAccessible(int POId, int UserId, int OrgId)
        {
            return (dbc.POHeaders.Where(c => c.CustID == OrgId && c.ID == POId).Count() > 0);
        }

        #endregion
    }

    public class CAWpo : _ServiceBase //: CAWBase - not needed because here we've kept the bulk Add, Edit & del function
    {
        #region Variables & Constructor
        public bool IsAsync { get; set; }/*return true;for testing */
        
        public CAWpo(bool Async){IsAsync = Async; }
        #endregion

        #region Add / Edit / Delete & Bulk
        
        public int AsyncBulkAddEditDelKO(vw_PO_Master_User_Loc vwObj, int StatusIDold, 
            IEnumerable<PODetail> items, IEnumerable<POComment> comments, IEnumerable<POFile> files)
        {
            PO poObj = POService.GetPOObjFromVW(vwObj);
            using (dbc)//Make sure this dbc is passed and persisted
            {
                bool isNewPO = (poObj.ID <= Defaults.Integer);
                bool doSubmit = true;
                string Progress = "";

                #region Set Transaction
                
                dbc.Connection.Open();
                //System.Data.Common.DbTransaction 
                var txn = dbc.Connection.BeginTransaction();
                dbc.Transaction = txn;
                //ExecuteReader requires the command to have a transaction when the connection assigned to the
                //command is in a pending local transaction. The Transaction property of the command has not been initialized.
                #endregion

                try
                {
                    Progress = 
                        "PO (" + poObj.ID + ", " + poObj.POGUID + ", " + poObj.PODate.ToString() + ")";
                    //Update po
                    new POService(dbc).AddEdit(poObj, StatusIDold, true);//doSubmit must be TRUE
                    //IMP: Note: The above addedit will return updated POObj which will have PO Id

                    Progress = "Comments";//Process comments
                    if(comments != null && comments.Count() > 0)
                        new CommentService(dbc).BulkAddEditDel(comments.ToList(), poObj.ID, doSubmit);
                    Progress = "HeaderFiles";//Process files (header) and files
                    if (files != null && files.Count() > 0)
                        new FileHeaderService(dbc).BulkAddEditDel(files.ToList(), poObj, doSubmit, dbc);
                    Progress = "POdetails";//Process items (and internally also process files(details)
                    
                    //NOTE: For Async the Details files will have to be handled internally in the above function
                    //EXTRA : Delete D_Temp folder ?
                    if (poObj.ID.ToString() != poObj.POGUID &&
                        !string.IsNullOrEmpty(poObj.POGUID))//ensure there's NO confusion
                        FileIO.EmptyDirectory(System.IO.Path.Combine(Config.UploadPath, poObj.POGUID.ToString()));

                    if (!doSubmit) dbc.SubmitChanges();//Make a FINAL submit instead of periodic updates
                    txn.Commit();//Commit
                }
                #region  Rollback if error
                catch (Exception ex)
                {
                    txn.Rollback();
                    Exception exMore = new Exception(ex.Message + " After " + Progress);
                    throw exMore;
                }
                finally
                {
                    if (dbc.Transaction != null)
                        dbc.Transaction.Dispose();
                    dbc.Transaction = null;
                }
                #endregion
            }           

            return poObj.ID;//Return updated poobj
        }

        #endregion
    }
}
