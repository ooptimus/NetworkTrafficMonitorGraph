using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using PacketDotNet;
using SharpPcap;
using System.Collections;
using System.Windows.Forms.DataVisualization.Charting;

namespace MyPacketCapturer
{
    public partial class frmCapture : Form
    {

        CaptureDeviceList devices;   //List of devices for this computer
        public static ICaptureDevice device; //The device we will be using
        public static string stringPackets = ""; //Data that is captured
        static int numPackets = 0;
        string filter = "ip and tcp"; //filter string
        static int length = 0; //size of packet
        static int count = 0; //tracking size of dictionary
        public static string sIP = ""; //source IP address of packet
        public static string dIP = ""; //destination IP aaddress of packet
        //create a dictionary to store IP addresses and sizes of packets
        public static Dictionary<string, int> IPvalues = new Dictionary<string, int>();


//**********Default constructor
public frmCapture()
        {
            InitializeComponent();
            
            //Get the list of devices
            devices = CaptureDeviceList.Instance;

            //Make sure that there is at least one device
            if (devices.Count < 1)
            {
                MessageBox.Show("No Capture Devices Found!!!");
                Application.Exit();
            }

            //Add the devices to the combo box
            foreach (ICaptureDevice dev in devices)
            {
                cmbDevices.Items.Add(dev.Description);
            }

            //Get the second device and display in combo box
            device = devices[0];
            cmbDevices.Text = device.Description;

            //Register our handler function to the 'packet arrival' event
            device.OnPacketArrival += new SharpPcap.PacketArrivalEventHandler(device_OnPacketArrival);

            //Open the device for capturing
            int readTimeoutMilliseconds = 1000;
            device.Open(DeviceMode.Promiscuous, readTimeoutMilliseconds);
            device.Filter = filter;
        } //End frmCapture


        //********** Event handler when a packet arrives
        private static void device_OnPacketArrival(object sender, CaptureEventArgs packet)
        {
            //Increment the number of packets captured
            numPackets++;

            //Put the packet number in the capture window
            stringPackets += "Packet Number: " + Convert.ToString(numPackets);
            stringPackets += Environment.NewLine;

            //Array to store our data
            byte[] data = packet.Packet.Data;

            //Keep track of the number of bytes displayed per line
            int byteCounter = 0;


            stringPackets += "Destination MAC Address: ";
            //Parsing the packets
            foreach (byte b in data)
            {
                //Add the byte to our string (in hexidecimal)
                if(byteCounter<=13||byteCounter>=34&&byteCounter<=37) stringPackets += b.ToString("X2") + " ";
                byteCounter++;
                length++; //increment lenght for each processed by byte
                switch (byteCounter)
                {
                    case 6: stringPackets += Environment.NewLine;
                        stringPackets += "Source MAC Address: ";
                        break;
                    case 12: stringPackets += Environment.NewLine;
                        stringPackets += "EtherType: ";
                        break;
                    case 14: if (data[12] == 8)
                        {
                            if (data[13] == 0) stringPackets += "(IP)";
                            if (data[13] == 6) stringPackets += "(ARP)";
                        }
                        break;
                    case 26: stringPackets += Environment.NewLine; //parsing source IP
                        stringPackets += "Source IP: ";
                        stringPackets += data[26] + "." + data[27] + "." + data[28] + "." + data[29];
                        sIP = data[26] + "." + data[27] + "." + data[28] + "." + data[29];
                        break;
                    case 30: stringPackets += Environment.NewLine; //parsing destination IP
                        stringPackets += "Destination IP: ";
                        stringPackets += data[30] + "." + data[31] + "." + data[32] + "." + data[33];
                        dIP = data[30] + "." + data[31] + "." + data[32] + "." + data[33];
                        break;
                    case 34: stringPackets += Environment.NewLine; //source port in hex
                        stringPackets += "Source Port: ";
                        break;
                    case 36: stringPackets += Environment.NewLine; //destination port in hex
                        stringPackets += "Destination Port: ";
                        break;
                }
            }

            stringPackets += Environment.NewLine;
            stringPackets += "Packet Length: " + length + " Bytes";
            stringPackets += Environment.NewLine + Environment.NewLine;

            //populate the dictionary with IP addresses and coresponding packet size
            //restrain recorded IPs to class B @ C networks to reduce graph size
            if (data[26] > 100)
            {
                if (!IPvalues.ContainsKey(sIP))
                {
                    IPvalues.Add(sIP, length);
                    count++;

                }
                else IPvalues[sIP] += length;
            }
            if(data[30] > 100)
            {
                if (!IPvalues.ContainsKey(dIP))
                {
                    IPvalues.Add(dIP, length);
                    count++;
                }
                else IPvalues[dIP] += length;
            }
            

            byteCounter = 0;
            length = 0;
            sIP = "";
            dIP = "";
        } //End device_OnPacketArrival


        //**********Starting and stopping the packet capturing
        private void btnStartStop_Click(object sender, EventArgs e)
        {
            try
            {
                if (btnStartStop.Text == "Start")
                {
                    btnStartStop.Text = "Stop";
                    device.StartCapture();
                    timer1.Enabled = true;
                  }
                else
                {
                    btnStartStop.Text = "Start";
                    device.StopCapture();
                    timer1.Enabled = false;
                }

            }
            catch (Exception exp) { MessageBox.Show("Error starting and stopping capture"); }
        } //End btnStartStop


        //**********Dumping the packet data from stringPackets to the text box
        private void timer1_Tick(object sender, EventArgs e)
        {
            txtCapturedData.AppendText(stringPackets);
            stringPackets = "";
            txtNumPackets.Text = Convert.ToString(numPackets);
            
            //write values of IP dictionary to ipList textbox
            var lines = IPvalues.Select(t => t.Key +"\t"+ t.Value);
            ipList.Text = string.Join(Environment.NewLine, lines);

        } //End timer1


        //**********Changing devices
        private void cmbDevices_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (device != null) device.Close();
            device = devices[cmbDevices.SelectedIndex];
            cmbDevices.Text = device.Description;
            txtGUID.Text = device.Name;

            //Register our handler function to the 'packet arrival' event
            device.OnPacketArrival += new SharpPcap.PacketArrivalEventHandler(device_OnPacketArrival);

            //Open the device for capturing
            int readTimeoutMilliseconds = 1000;
            device.Open(DeviceMode.Promiscuous, readTimeoutMilliseconds);
        } //End cmbDevices_SelectedIndexChanged

 
        //**********Saving the file
        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            saveFileDialog1.Filter = "Text Files|*.txt|All Files|*.*";
            saveFileDialog1.Title = "Save the Captured Packets";
            saveFileDialog1.ShowDialog();

            //Check to see if a filename was given
            if (saveFileDialog1.FileName != "")
            {
                System.IO.File.WriteAllText(saveFileDialog1.FileName, txtCapturedData.Text);
            }
        } //End saveToolStripMenuItem_Click


        //**********Openning the file
        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            openFileDialog1.Filter = "Text Files|*.txt|All Files|*.*";
            openFileDialog1.Title = "Open Captured Packets";
            openFileDialog1.ShowDialog();

            //Check to see if a filename was given
            if (openFileDialog1.FileName != "")
            {
                txtCapturedData.Text=System.IO.File.ReadAllText(openFileDialog1.FileName);
            }
        } //End openToolStripMenuItem_Click

        //**********Clear the main textbox
        private void clearToolStripMenuItem_Click(object sender, EventArgs e)
        {
            txtCapturedData.Clear();
            ipList.Clear();
            numPackets = 0;
            txtNumPackets.Text = "0";
        }  //End learToolStripMenuItem_Click

        private void frmCapture_Load(object sender, EventArgs e)
        {
            dataTraffic.ChartAreas["ChartArea1"].AxisX.Interval = 1;
        }

        private void chartBtn_Click(object sender, EventArgs e)
        {
            //assign the IP addresses and packet sizes from the dictionary to the chart

            try {
                if (chartBtn.Text == "Display Chart")
                {
                    chartBtn.Text = "Clear Chart";
                    Series ser1 = new Series(" Network\n Traffic\n Per IP \n In Bytes", 10);
                    dataTraffic.Series.Add(ser1);
                    dataTraffic.Series[" Network\n Traffic\n Per IP \n In Bytes"].Points.DataBindXY(IPvalues.Keys, IPvalues.Values);
                }
                else {
                    chartBtn.Text = "Display Chart";
                    dataTraffic.Series.Clear();
                    IPvalues.Clear();
                }
            }catch(Exception exp) { MessageBox.Show("Problem With Chart Display"); }
        }
    }
}
