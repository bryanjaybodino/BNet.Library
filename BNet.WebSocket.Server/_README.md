
Complete Standard FTP Server using C#

            
Command List 

PWD
CWD
PASV
LIST
STOR
RETR
DELE
RMD
MKD
RNFR
RNTO
QUIT
NOOP
TYPE
SITE
OPTS
PORT





    internal class Program
    {

        static Commands commands = new Commands("C://", 2022);
        static async Task Main(string[] args)
        {
            /// THIS CODES IS NOT RELATED TO THE LIBRARY YOU CAN REMOVE THIS IF YOU WANT
            Console.WriteLine("LISTING DOWN ALL THE IP ADDRESS OF THE SERVER");
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus == OperationalStatus.Up)
                {
                    IPInterfaceProperties properties = ni.GetIPProperties();
                    foreach (UnicastIPAddressInformation ip in properties.UnicastAddresses)
                    {
                        Console.WriteLine(" --> ftp://" + ip.Address.ToString() + ":" + 2022);
                    }
                }
            }
            Console.WriteLine();
            //START SERVICE
            await StartService();
        }


        ///YOU CAN ALSO UPDATE THE PORT AND DIRECTORY
        static async Task UpdateAsync()
        {
            await StopService();

            string NewDirectory = "D://";
            int NewPort = 2023;
            commands.Setup(NewDirectory, NewPort);

            await StartService();
        }

        //OPTIONAL CODES IF NO UserCredentials It is Anonymous
        static void CreateCredentials()
        {
            commands.UserCredentials.Add("bryan", "12345");
            commands.UserCredentials.Add("pogi", "ako");
        }

        //STOP SERVICE
        static async Task StopService()
        {
            await commands.StopAsync();
        }

        //START SERVICE
        static async Task StartService()
        {
            await commands.StartAsync();
        }
    }





<img width="960" alt="Screenshot FTP" src="https://github.com/user-attachments/assets/1a28fedb-512c-4dbb-b997-db8284b2b25a">


