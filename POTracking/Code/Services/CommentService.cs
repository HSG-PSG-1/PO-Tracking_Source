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
    public class CommentService : _ServiceBase
    {
        #region Variables & Constructor
        
        public readonly POComment newObj = new POComment() { ID = Defaults.Integer };
        
        public CommentService() : base() {;}
        public CommentService(POTmodel dbcExisting) : base(dbcExisting) { ;}

        #endregion

        #region Search / Fetch

        public List<POComment> Search(int poID, int? userID)
        {
            //using (dbc)//HT: DON'T coz we're sending IQueryable
            IQueryable<POComment> cQuery = from c in dbc.POComments
                                         join u in dbc.Users on new { UserID = c.UserID } equals new { UserID = u.ID } into u_join
                                         from u in u_join.DefaultIfEmpty()
                                         where c.POID == poID
                                         orderby c.PostedOn descending
                                         select Transform(c,u.Name, c.POID);

            //Append WHERE clause if applicable
            //if ((userID ?? 0) > 0) cQuery = cQuery.Where(o => o.UserID == userID.Value);

            return cQuery.ToList<POComment>();
        }

        POComment Transform(POComment c, string commentBy, int poID)
        {
            return c.Set(c1 => { c1.CommentBy = commentBy;                 
                /* IMP: NOTE - The following is NOT needed because we've set the Association Access to Internal in the dbml
                 c1.PO = null; c1.POID1 = poID; */
            });
        }
                
        /*public POComment GetCommentById(int id)
        {
            using (dbc)
            {
                POComment cmt = (from c in dbc.POComments where c.ID == id select c).SingleOrDefault<POComment>();

                if (cmt == null) return newObj;
                cmt.PO = new PO();//HT: So that it doesn't complain NULL later
                return cmt;
            }
        }*/

        #endregion
    }
}