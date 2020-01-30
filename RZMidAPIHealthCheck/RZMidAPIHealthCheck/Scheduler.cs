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
            string stillaliveOne = ConfigurationManager.AppSettings["stillaliveone"].ToString();
            string stillaliveTwo = ConfigurationManager.AppSettings["stillalivetwo"].ToString();
            string conString = ConfigurationManager.AppSettings["PortalEmailerConnectionString"].ToString();

            // Create an HttpClient instance
            HttpClient clientOne = new HttpClient();
            clientOne.BaseAddress = new Uri(stillaliveOne);

            strEmail = "------------------------------------------------------------ \n";

            strEmail = strEmail + "MidAPI STATUS \n";

            strEmail = strEmail + "------------------------------------------------------------\n\n";

            strEmail = strEmail + "JOB RUN:  " + DateTime.Now + "\n";

            strEmail = strEmail + "STATUS:   Failed \n";
            //Call stillaliveOne endpoint
            try
            {
                HttpResponseMessage response = clientOne.GetAsync("heartbeat/v1/stillalive").Result;
                var res = response.Content.ReadAsStringAsync();
                var root = JsonValue.Parse(res.Result);
                string status = root["StillAlive"].ToString();
                if (status.Replace("\"", "") != "yes")
                {
                    strEmail = strEmail + "MESSAGES: The middleware server " + stillaliveOne + "heartbeat/v1/stillalive is down. \n";
                }

                //Call stillaliveTwo endpoint
                HttpClient clientTwo = new HttpClient();
                clientTwo.BaseAddress = new Uri(stillaliveTwo);
                HttpResponseMessage resp = clientTwo.GetAsync("heartbeat/v1/stillalive").Result;
                var read = resp.Content.ReadAsStringAsync();
                var parse = JsonValue.Parse(read.Result);
                string statusTwo = parse["StillAlive"].ToString();
                if (statusTwo.Replace("\"", "") != "yes")
                {
                    strEmail = strEmail + "MESSAGES: The middleware server " + stillaliveTwo + "heartbeat/v1/stillalive is down. \n";
                }

                if ((status.Replace("\"", "") != "yes") || (statusTwo.Replace("\"", "") != "yes"))
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
