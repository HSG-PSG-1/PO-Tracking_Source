using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using POT.DAL;
using POT.Models;
using POT.Services;
using System.Web.Security;

namespace HSG.Helper
{
    public class _Session
    {
        const string sep = ";";

        #region Object (class) containing session objects

        /* Obsolete - kept for future ref - public static UserSession Usr
        { // SO: 11955094 Go back to In proc session
            get
            {
                try
                {
                    string byteArrStr = HttpContext.Current.Session["UserObj"].ToString();
                    if (string.IsNullOrEmpty(byteArrStr))
                        return new UserService().emptyView;
                    return Serialization.Deserialize<vw_Users_Role_Org>(byteArrStr);
                }
                catch { return new UserService().emptyView; }
            }
            set {
                if (value == null) return;//Extra worst-case check
                //HttpContext.Current.Session["UserObj"] = Serialization.Serialize<vw_Users_Role_Org>(value);
                UserSession.setUserSession(value);
            }
        }*/

        public static UserRole RoleRights
        {
            get
            {
                UserRole rDataEmpty = new UserRole();
                try
                {
                    string byteArrStr = HttpContext.Current.Session["RoleRights"].ToString();
                    if (string.IsNullOrEmpty(byteArrStr))
                        return rDataEmpty;

                    return Serialization.Deserialize<UserRole>(byteArrStr);
                }
                catch { return rDataEmpty; }
            }
            set
            {
                if (value == null) return;//Extra worst-case check
                HttpContext.Current.Session["RoleRights"] = Serialization.Serialize<UserRole>(value);
            }
        }

        public static MasterService.Table? MasterTbl
        {
            get
            {
                if (HttpContext.Current.Session["MasterTbl"] == null) return null;
                else return _Enums.ParseEnum<MasterService.Table>(HttpContext.Current.Session["MasterTbl"]);
            }
            set { HttpContext.Current.Session["MasterTbl"] = value; }
        }

        public static Filters Search { get { return new Filters(); } }

        #endregion

        #region Security objects

        public static bool IsInternal
        {
            get
            {
                try { return IsAdmin || (_SessionUsr.OrgTypeId == (int)OrgService.OrgType.Internal); }
                catch { return false; }
            }            
        }

        public static bool IsAdmin
        {
            get
            {
                try { return (_SessionUsr.OrgTypeId == (int)OrgService.OrgType.Internal); }
                catch { return false; }
            }
        }

        public static bool IsSales
        {
            get
            {
                try { return IsAdmin || (_SessionUsr.RoleID == (int)SecurityService.Roles.Sales); }
                catch { return false; }
            }
        }

        public static bool IsOnlyCustomer
        {
            get
            {
                try { return (_SessionUsr.OrgTypeId == (int)OrgService.OrgType.Customer); }
                catch { return false; }
            }
        }

        public static bool IsOnlyVendor
        {
            get
            {
                try { return (_SessionUsr.RoleID == (int)SecurityService.Roles.Vendor); }
                catch { return false; }
            }
        }

        public static bool IsOnlySales
        {
            get
            {
                try { return (_SessionUsr.RoleID == (int)SecurityService.Roles.Sales); }
                catch { return false; }
            }
        }

        #endregion
        
        /*#region PO related
        
        public static PO PO1
        {
            get
            {
                try
                {
                    string byteArrStr = HttpContext.Current.Session["POObj"].ToString();
                    if (string.IsNullOrEmpty(byteArrStr))
                        return new POService().emptyPO;
                    return Serialization.Deserialize<PO>(byteArrStr);
                }
                catch { return new POService().emptyPO; }
            }
            set
            {
                if (value == null) return;//Extra worst-case check
                //Set here to avoid replication and maintain at a single location
                if (string.IsNullOrEmpty(value.POGUID)) //initiate GUID - if not done already (OBSOLETE NOW)
                    value.POGUID = System.Guid.NewGuid().ToString();
                HttpContext.Current.Session["POObj"] = Serialization.Serialize<PO>(value);
            }
        }

        public static POs POs { get{return new POs();} }

        public static void ResetPOInSessionAndEmptyTempUpload(string POGUID)
        { // Use POGUID to find the exact po from
            if (!string.IsNullOrEmpty(POGUID)) // HT: ENSURE POGUID is present
                FileIO.EmptyDirectory(System.IO.Path.Combine(Config.UploadPath, POGUID));
            
            POs.Remove(POGUID); // Remove the PO from session
            //HttpContext.Current.Session.Remove("POObj");
        }

        #endregion */

        #region Misc & functions

        public static string OldSort1
        {
            get
            {
                try { return (HttpContext.Current.Session["OldSort"] ?? "").ToString().Trim(); }
                catch { return ""; }//DON'T forget to trim
            }
            set
            {
                HttpContext.Current.Session["OldSort"] = value;
            }
        }

        public static string NewSort1
        {
            get
            {
                try { return (HttpContext.Current.Session["NewSort"] ?? "").ToString().Trim(); }
                catch { return ""; }//DON'T forget to trim
            }
            set
            {
                HttpContext.Current.Session["NewSort"] = value;
            }
        }

        public static bool IsValid(HttpContext ctx)
        {/*See in future if need more deep validation 
            if (!string.IsNullOrEmpty((ctx.Session["UserObj"] ?? "").ToString()))
                return (_SessionUsr != new UserService().emptyView);

            return false;*/
            return _SessionUsr.ID > 0;
        }

        public static void Signout()
        {
            FormsAuthentication.SignOut();//HT: reset forms authentication!

            #region clear authentication cookie
            // Get all cookies with the same name
            string[] cookies = new string[] { Defaults.cookieName, Defaults.emailCookie, Defaults.passwordCookie };
            
            //Iterate for each cookie and remove
            foreach (string cookie in HttpContext.Current.Request.Cookies.AllKeys)
                if (!cookies.Contains(cookie))
                    HttpContext.Current.Request.Cookies.Remove(cookie);
            // Strange but it is needed to do it the second time
            foreach (string cookie in HttpContext.Current.Response.Cookies.AllKeys)
                if (!cookies.Contains(cookie))
                    HttpContext.Current.Response.Cookies.Remove(cookie);

            #endregion
            
            //Clear & Abandon session
            HttpContext.Current.Session.Clear();
            HttpContext.Current.Session.Abandon();
        }

        public static string ErrDetailsForELMAH
        {
            get
            {
                try { return (HttpContext.Current.Session["ErrDetailsForELMAH"] ?? "").ToString(); }
                catch { return ""; }
            }
            set
            {
                HttpContext.Current.Session["ErrDetailsForELMAH"] = value;
            }
        }

        public static string WebappVersion
        { // http://www.craftyfella.com/2010/01/adding-assemblyversion-to-aspnet-mvc.html
            get
            {
                if (string.IsNullOrEmpty((HttpContext.Current.Session["WebappVersion"] ?? "").ToString()))
                {
                    try
                    {
                        System.Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                        return version.Major + "." + version.Minor + "." + version.Build;
                    }
                    catch (Exception)
                    {
                        return "?.?.?";
                    }
                }
                else
                    return HttpContext.Current.Session["WebappVersion"].ToString();
            }
        }

        #endregion
    }

    /*public class POs
    {
        //http://stackoverflow.com/questions/287928/how-do-i-overload-the-square-bracket-operator-in-c
        //http://msdn.microsoft.com/en-us/library/2549tw02.aspx

        // Indexer declaration. 
        // If index is out of range, the array will throw the exception.         
        public PO this[string POGUID]
        {
            get
            {
                object data = HttpContext.Current.Session[POGUID];
                try
                {
                    if (string.IsNullOrEmpty(data.ToString()))//byteArrStr
                        return new POService().emptyPO;
                    return Serialization.Deserialize<PO>(data.ToString());//byteArrStr
                }
                catch { return new POService().emptyPO; }
                //foreach (PO clm in this)
                //    if (clm.POGUID == POGUID) return clm;                
            }
            set
            {
                if (!string.IsNullOrEmpty(value.POGUID))
                    HttpContext.Current.Session[value.POGUID] = Serialization.Serialize<PO>(value);
            }
        }

        public void Remove(string POGUID)
        {
            HttpContext.Current.Session.Remove(POGUID);
        }
    }*/

    [Serializable]
    public class Filters
    {//http://stackoverflow.com/questions/287928/how-do-i-overload-the-square-bracket-operator-in-c
        const string prefix = "ObjFor";
        public static readonly object empty = new object();
        public enum list
        {
            _None,
            Dashboard,
            ActivityLog,
            User
        }

        // Indexer declaration. 
        // If index is out of range, the array will throw the exception.
        public object this[Enum filterID]
        {
            get
            {
                object filterData = HttpContext.Current.Session[prefix + filterID.ToString()];
                try
                {
                    if (filterData == null || filterData == empty)
                    {
                        switch (_Enums.ParseEnum<list>(filterID))
                        {
                            case list.Dashboard: filterData = new DashboardService().emptyView; break;
                            case list.ActivityLog: filterData = new ActivityLogService(ActivityLogService.Activity.Login).emptyView; break;
                            case list.User: filterData = new UserService().emptyView; break;
                        }
                    }                 
                    return filterData;
                }
                catch { return null; }                
            }
            set
            {
                HttpContext.Current.Session[prefix + filterID.ToString()] = value;
            }
        }

        public void Remove(string filterID)
        {
            HttpContext.Current.Session.Remove(prefix + filterID);
        }
    }
}
