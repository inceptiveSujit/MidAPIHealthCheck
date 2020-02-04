using System;
using System.Data;
using System.Net.Http;
using System.ServiceProcess;
using System.Timers;
using System.Configuration;
using System.Net.Mail;
using System.Net;
using System.Json;
using System.Data.SqlClient;
using System.Collections.Specialized;
using System.Collections.Generic;

namespace RZMidAPIHealthCheck
{
    public partial class Scheduler : ServiceBase
    {
        private Timer timer = null;
        public Scheduler()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            // Set up a timer that triggers every minute.
            timer = new Timer();
            this.timer.Interval = 60000; // 60 seconds
            this.timer.Elapsed += new System.Timers.ElapsedEventHandler(this.timer_Tick);
            timer.Enabled = true;
        }

        public void timer_Tick(object sender, ElapsedEventArgs args)
        {
            string strEmail = "";
            string conString = ConfigurationManager.AppSettings["PortalEmailerConnectionString"].ToString();
            string[] urlArr = ConfigurationManager.AppSettings["stillalive"].Split(';');
            
            //Call stillaliveOne endpoint
            try
            {
                foreach (var url in urlArr)
                {
                    // Create an HttpClient instance
                    HttpClient client = new HttpClient();
                    HttpResponseMessage response = client.GetAsync(url).Result;
                    var res = JsonValue.Parse(response.Content.ReadAsStringAsync().Result);
                    string status = res["StillAlive"].ToString();
                    
                    if (status.Replace("\"", "")  != "yes")
                    {
                        //Check if email header is already present in string email
                        if (strEmail == "")
                        {
                            strEmail = "------------------------------------------------------------ \n";

                            strEmail = strEmail + "MidAPI STATUS \n";

                            strEmail = strEmail + "------------------------------------------------------------\n\n";

                            strEmail = strEmail + "JOB RUN:  " + DateTime.Now + "\n";

                            strEmail = strEmail + "STATUS:   Failed \n";
                        }
                        strEmail = strEmail + "MESSAGES: The middleware server " + url + " is down. \n";
                    }
                }
                
                //Check if server is live and schedule email
                if (strEmail != "")
                {
                    //SCHEDULE EMAIL
                    SqlConnection conn = new SqlConnection(conString);
                    conn.Open();

                    //Call stored procedure to schedule email
                    SqlCommand cmd = new SqlCommand("spu_MidAPI_Health_Check", conn);
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@Email_Body", SqlDbType.VarChar).Value = strEmail;
                    cmd.ExecuteNonQuery();
                    conn.Close();
                }
            }
            catch (Exception ex)
            {
                //Check if email header is already present in string email
                if (strEmail == "")
                {
                    strEmail = "------------------------------------------------------------ \n";

                    strEmail = strEmail + "MidAPI STATUS \n";

                    strEmail = strEmail + "------------------------------------------------------------\n\n";

                    strEmail = strEmail + "JOB RUN:  " + DateTime.Now + "\n";

                    strEmail = strEmail + "STATUS:   Failed \n";
                }
                strEmail = strEmail + "MESSAGES: The job failed. Exception occured. " + ex + " \n";
                SqlConnection conn = new SqlConnection(conString);
                conn.Open();

                //Call stored procedure to schedule email
                SqlCommand cmd = new SqlCommand("spu_MidAPI_Health_Check", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("@Email_Body", SqlDbType.VarChar).Value = strEmail;
                cmd.ExecuteNonQuery();
                conn.Close();
            }
        }

        protected override void OnStop()
        {
            timer.Enabled = false;
        }
    }
}
