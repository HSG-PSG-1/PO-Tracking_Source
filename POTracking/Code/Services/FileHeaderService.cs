using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic;
using System.Data.Linq.SqlClient;
using POT.DAL;
using HSG.Helper;
using Webdiyer.WebControls.Mvc;

namespace POT.Services
{
    public class POFileService : _ServiceBase
    {
        #region Variables & Constructor
        
        public readonly POFile newObj = new POFile() { ID = Defaults.Integer };

        public POFileService() : base() {;}
        public POFileService(POTmodel dbcExisting) : base(dbcExisting) { ;}
        
        #endregion

        #region Search / Fetch

        public List<POFile> Search(int poID, int? userID)
        {
            //using (dbc)//HT: DON'T coz we're sending IQueryable
            IQueryable<POFile> cQuery = from f in dbc.POFiles                                            
                                            
                                            #region LEFT OUTER JOINs
                                                
                                                //LEFT OUTER JOIN For User
                                            join u in dbc.Users on new { UserID = f.UserID } equals
                                            new { UserID = u.ID } into u_join
                                            from u in u_join.DefaultIfEmpty()

                                            //LEFT OUTER JOIN For User
                                            join t in dbc.MasterFileTypes on
                                         new { TypID = f.FileType.Value } equals new { TypID = t.ID } into t_join
                                            from t in t_join.DefaultIfEmpty()
                                                
                                                #endregion

                                            where f.POID == poID
                                            orderby f.UploadDate descending
                                            select Transform(f, u.Name, t.Code, f.POID);

            return cQuery.ToList<POFile>();
        }

        POFile Transform(POFile f, string fileHeaderBy, string fileTypeTitle, int poID)
        {
            /*also set .PO = null to avoid issues during serialization but persist POID as POID1 */
            return f.Set(f1 =>
            {
                f1.UploadedBy = fileHeaderBy; f1.FileTypeTitle = fileTypeTitle; f1.POGUID = f1.POID.ToString(); 
                /*f1.PO = null; f1.POID1 = poID;
                 NOT needed because we've set the Association Access to Internal in the dbml*/});
        }
                
        public POFile GetPOFileById(int id)
        {
            using (dbc)
            {
                POFile cmt = (from f in dbc.POFiles where f.ID == id select f).SingleOrDefault<POFile>();
                //cmt.PO = new PO();//HT: So that it doesn't complain NULL later
                return cmt;
            }
        }

        #endregion
                
        
    }
}
