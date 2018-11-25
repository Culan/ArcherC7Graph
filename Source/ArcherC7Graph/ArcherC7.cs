using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace ArcherC7Graph
{
    class ArcherC7
    {
        private HttpClient client = new HttpClient();

        private static string resetStatsUrl = "userRpm/SystemStatisticRpm.htm?ResetAll=All&interval=10&autoRefresh=0&sortType=3&Num_per_page=100&Goto_page=1";
        private static string getStatsUrl = "userRpm/SystemStatisticRpm.htm?interval=10&autoRefresh=0&Refresh=Refresh&sortType=3&Num_per_page=100&Goto_page=1";
        private static string getDhcpUrl = "userRpm/AssignedIpAddrListRpm.htm";

        private readonly CookieContainer _cookies = new CookieContainer();
        private readonly IPAddress _ip = IPAddress.Parse("192.168.0.1");
        public string Session { get; private set; }

        public string Username = "admin";
        public string Password = "";

        public Dictionary<string, string> dhcpClients = new Dictionary<string, string>();
        public Dictionary<string, List<string>> stats = new Dictionary<string, List<string>>();

        private object syncObject = new object();

        public bool LoggedIn { get; private set; }

        public ArcherC7()
        {
        }

        public void ClearStats()
        {
            DoRequest(resetStatsUrl);
        }

        public void getDhcp()
        {
            ParseDhcpList(DoRequest(getDhcpUrl));
        }

        public void ParseDhcpList(string response)
        {
            dhcpClients.Clear();
            string array = getBetween(response, "new Array(", ");");

            string[] dhcpArr = array.Split(',');

            int index = 0;

            while (index < dhcpArr.Length-2)
            {
                dhcpClients.Add(dhcpArr[index + 1], dhcpArr[index]);
                index += 4;
            }
        }

        public static string getBetween(string strSource, string strStart, string strEnd)
        {
            int Start, End;
            if (strSource.Contains(strStart) && strSource.Contains(strEnd))
            {
                Start = strSource.IndexOf(strStart, 0) + strStart.Length;
                End = strSource.IndexOf(strEnd, Start);
                return strSource.Substring(Start, End - Start);
            }
            else
            {
                return "";
            }
        }

        public List<KeyValuePair<string, string>> getStats()
        {
            string result = DoRequest(getStatsUrl);
            return ParseStats(result);
            //return result;
        }

        public List<KeyValuePair<string, string>> ParseStats(string response)
        {
            string array = getBetween(response, "new Array(", ");");
            string[] statArr = array.Split(',');
            int index = 0;

            List<KeyValuePair<string, string>> result = new List<KeyValuePair<string, string>>(); 
            
            while (index < statArr.Length - 2)
            {
                // Dhcp name found
                if (dhcpClients.Keys.Contains(statArr[index + 2]))
                {
                    result.Add(new KeyValuePair<string, string>(dhcpClients[statArr[index + 2]], statArr[index + 6]));
                }
                // Dhcp name not found
                else
                {
                    result.Add(new KeyValuePair<string, string>(statArr[index + 2], statArr[index + 6]));
                }

                index += 13;
            }
            return result;
        }

        public bool Login()
        {
            if ((Username.Length > 0) && (Password.Length > 0))
            {
                var md5 = string.Join("", MD5.Create().ComputeHash(Encoding.Default.GetBytes(Password)).Select(x => x.ToString("x2")));
                var auth = Convert.ToBase64String(Encoding.Default.GetBytes(Username + ":" + md5));
                _cookies.Add(new Cookie("Authorization", "Basic " + auth, "/", _ip.ToString()));

                var response = DoRequest("userRpm/LoginRpm.htm?Save=Save");
                var match = Regex.Match(response, "http://" + _ip + "/(.+)/userRpm");
                if (match.Success)
                {
                    Session = match.Groups[1].Value;
                    LoggedIn = true;
                    return LoggedIn;
                }
            }
            else
            {
                MessageBox.Show("Can not login with empty username or password.");
            }
            return LoggedIn;
        }

        public void Logout()
        {
            DoRequest("userRpm/LogoutRpm.htm");
            LoggedIn = false;
        }

        private string DoRequest(string url)
        {
            lock (syncObject)
            {
                var request = WebRequest.CreateHttp("http://" + _ip + "/" + (Session != null ? Session + "/" : "") + url);

                request.CookieContainer = _cookies;
                request.AllowAutoRedirect = true;
                request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/45.0.2454.101 Safari/537.36";
                request.Referer = "http://" + _ip + "/";

                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        throw new Exception("HTTP error code " + (int)response.StatusCode + " from " + url);
                    }

                    using (var stream = response.GetResponseStream())
                    {
                        if (stream == null)
                        {
                            return null;
                        }

                        var buffer = new byte[1024];
                        var sb = new StringBuilder();

                        int len;

                        do
                        {
                            len = stream.Read(buffer, 0, buffer.Length);
                            sb.Append(Encoding.Default.GetString(buffer, 0, len));
                        } while (len != 0);

                        var result = sb.ToString();

                        var errMatch = Regex.Match(result, "errCode\\s*=\\s*\"(\\d+)\"");
                        if (errMatch.Success)
                        {
                            throw new Exception("Error " + errMatch.Groups[1].Value + " while configuring router");
                        }

                        return result;
                    }
                }
            }
        }
    }
}
