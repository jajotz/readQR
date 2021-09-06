using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;

using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using AForge.Video.DirectShow;
using BarcodeLib.BarcodeReader;
using System.Data.OleDb;
using System.IO.Ports;

namespace ReadQRcode
{
    public partial class FormGuestCheckin : Form
    {
        private char[] SEPARATOR = { '#' };
        private const String DATETIME_FORMAT = "yyyy-MM-dd HH:mm:ss";
        private const String DATE_FORMAT = "yyyy-MM-dd";
        private const String TIME_FORMAT = "HH:mm:ss";
        private const String DATACHECKIN_FILE_PATH = @"Data\CheckInGuestList.txt";
        private const String DATABASE_FILE_PATH = @"Data\Database.txt";

        FilterInfoCollection ball;
        VideoCaptureDevice fuenteVideo;

        String _ResultTemp = "";
        DateTime _TimeStamp;

        IList<GuestModel> listAllGuest;
        IList<GuestModel> listCheckInGuest;

        public FormGuestCheckin()
        {
            InitializeComponent();
        }

        private IList<GuestModel> GetAllGuestFromDB()
        {
            var list = new List<GuestModel>();

            try
            {
                if (System.IO.File.Exists(DATABASE_FILE_PATH))
                {
                    string[] lines = System.IO.File.ReadAllLines(DATABASE_FILE_PATH);

                    foreach (string line in lines)
                    {
                        var obj = DeserializeObj(line);
                        list.Add(obj);
                    }
                }
                else
                {
                    System.IO.File.Create(DATABASE_FILE_PATH);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

            return list;
        }

        private IList<GuestModel> GetCheckInGuestFromDB()
        {
            var list = new List<GuestModel>();

            try
            {
                if (System.IO.File.Exists(DATACHECKIN_FILE_PATH))
                {
                    string[] lines = System.IO.File.ReadAllLines(DATACHECKIN_FILE_PATH);

                    foreach (string line in lines)
                    {
                        var obj = DeserializeObj(line);
                        list.Add(obj);
                    }
                }
                else
                {
                    System.IO.File.Create(DATACHECKIN_FILE_PATH);
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

            return list;
        }

        private void GenerateColumn()
        {
            dataGridView1.Columns.Add("CheckInDateText", "Check In");
            dataGridView1.Columns.Add("GuestId", "Id");
            dataGridView1.Columns.Add("GuestName", "Name");
            dataGridView1.Columns[0].Width = 60;
            dataGridView1.Columns[0].DataPropertyName = "CheckInDateText";
            dataGridView1.Columns[1].Width = 40;
            dataGridView1.Columns[1].DataPropertyName = "GuestId";
            dataGridView1.Columns[2].Width = 172;
            dataGridView1.Columns[2].DataPropertyName = "GuestName";
        }

        private void GuestSignin_Load(object sender, EventArgs e)
        {
            timer2.Start();

            listAllGuest = GetAllGuestFromDB();
            listCheckInGuest = GetCheckInGuestFromDB();
            dataGridView1.AutoGenerateColumns = false;
            dataGridView1.DataSource = listCheckInGuest.OrderByDescending(x => x.CheckInDate).ToList();
            GenerateColumn();

            btnStop.Enabled = false;
            lblTime.Visible = false;
            lblGreeting.Visible = false;

            ball = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            foreach (FilterInfo x in ball)
            {
                comboScanner.Items.Add(x.Name);
            }
            comboScanner.SelectedIndex = 0;
        }

        private void btnCapture_Click(object sender, EventArgs e)
        {
            if (videoSourcePlayer1.GetCurrentVideoFrame() != null)
            {
                if (pictureBox1.Image != null)
                {
                    pictureBox1.Image.Dispose();
                    pictureBox1.Image = null;
                }

                Bitmap img = new Bitmap(videoSourcePlayer1.GetCurrentVideoFrame());
                pictureBox1.Image = img;

            }
        }
        private void btnScan_Click(object sender, EventArgs e)
        {
            if (pictureBox1.Image != null)
            {
                var oriImg = pictureBox1.Image.Clone();
                Bitmap img = (Bitmap)pictureBox1.Image;
                ScanImage(img);
                img.Dispose();
                pictureBox1.Image = (Bitmap)oriImg;
            }
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            btnStart.Enabled = false;
            btnStop.Enabled = true;
            comboScanner.Enabled = false;

            timer1.Enabled = true;
            timer1.Start();
            fuenteVideo = new VideoCaptureDevice(ball[comboScanner.SelectedIndex].MonikerString);
            videoSourcePlayer1.VideoSource = fuenteVideo;
            videoSourcePlayer1.Start();
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            timer1.Stop();

            if (fuenteVideo != null && fuenteVideo.IsRunning)
            {
                fuenteVideo.Stop();
            }

            _ResultTemp = "";
            lblTime.Visible = false;
            lblGreeting.Visible = false;
            btnStart.Enabled = true;
            comboScanner.Enabled = true;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            try
            {
                if (videoSourcePlayer1.GetCurrentVideoFrame() != null)
                {
                    Bitmap img = new Bitmap(videoSourcePlayer1.GetCurrentVideoFrame());
                    ScanImage(img);
                    img.Dispose();
                }
            }
            catch { }

        }

        private void GuestSignin_FormClosing(object sender, FormClosingEventArgs e)
        {
            var confirmResult = MessageBox.Show("Are you sure to exit?",
                                     "Confirm Exit",
                                     MessageBoxButtons.YesNo);
            if (confirmResult == DialogResult.Yes)
            {
                if (fuenteVideo != null && fuenteVideo.IsRunning)
                {
                    fuenteVideo.Stop();
                }
            }
            else
            {
                e.Cancel = true;
            }

        }

        private String SerializeObj(GuestModel obj)
        {
            if (obj != null)
            {
                return String.Format("#{0}#{1}#{2}", obj.CheckInDate.ToString(DATETIME_FORMAT), obj.GuestId, obj.GuestName);
            }
            else
            {
                return "";
            }
        }
        private GuestModel DeserializeObj(String serializeObj)
        {
            try
            {
                var split = serializeObj.Split(SEPARATOR, StringSplitOptions.RemoveEmptyEntries);

                var timeStamp = DateTime.Now;
                var guestId = "";
                var guestName = "";
                if (split.Count() == 2)
                {
                    guestId = split[0];
                    guestName = split[1];
                }
                else
                {
                    timeStamp = DateTime.ParseExact(split[0], DATETIME_FORMAT, System.Globalization.CultureInfo.InvariantCulture);
                    guestId = split[1];
                    guestName = split[2];
                }

                var obj = new GuestModel(timeStamp, guestId, guestName);

                return obj;
            }
            catch (Exception ex) { throw ex; }
            return null;
        }

        private bool InsertToDB(GuestModel obj)
        {
            try
            {
                using (System.IO.StreamWriter file = new System.IO.StreamWriter(DATACHECKIN_FILE_PATH, true))
                {
                    file.WriteLine(SerializeObj(obj));
                }

                return true;
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
            return false;
        }

        private void ScanImage(Bitmap img)
        {
            using (img)
            {
                string[] resultados = BarcodeReader.read(img, BarcodeReader.QRCODE);
                string result = "";
                if (resultados != null)
                {
                    result = resultados[0];
                    result = result.Remove(0, 1);

                    if (result.StartsWith(SEPARATOR[0].ToString()) && !_ResultTemp.Equals(result))
                    {
                        _TimeStamp = DateTime.Now;
                        _ResultTemp = result;
                        var obj = DeserializeObj(_ResultTemp);
                        if (obj != null)
                        {
                            ProcessCheckIn(obj);
                        }


                    }

                }
            }

        }

        private bool ProcessCheckIn(GuestModel obj)
        {
            if (!listCheckInGuest.Any(x => x.GuestId == obj.GuestId))
            {
                if (InsertToDB(obj))
                {
                    listCheckInGuest.Add(obj);


                    dataGridView1.DataSource = null;
                    dataGridView1.DataSource = listCheckInGuest.OrderByDescending(x => x.CheckInDate).ToList();

                    Console.Beep();

                    lblTime.Text = obj.CheckInDate.ToString(TIME_FORMAT);
                    lblGreeting.Text = "Welcome, " + Environment.NewLine + obj.GuestName;

                    lblTime.Visible = true;
                    lblGreeting.Visible = true;

                    return true;
                }
            }
            else
            {
                Console.Beep();
                MessageBox.Show("GuestId: " + obj.GuestId + Environment.NewLine + "Guest Name: " + obj.GuestName, "Already Checkin");
                return true;
            }

            return false;
        }

        private void timer2_Tick(object sender, EventArgs e)
        {
            lblDate.Text = DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss");
        }

        private void cbManualInput_CheckedChanged(object sender, EventArgs e)
        {
            panelManualInput.Visible = cbManualInput.Checked;
        }

        private void btnManualInputOK_Click(object sender, EventArgs e)
        {
            InputManual(txtID.Text);
        }

        private void txtID_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                InputManual(txtID.Text);
            }
        }

        private void InputManual(String id)
        {
            if (String.IsNullOrEmpty(id.Trim())) return;

            if(listAllGuest != null)
            {
                var obj = listAllGuest.Where(x => x.GuestId.Equals(id.Trim(), StringComparison.CurrentCultureIgnoreCase)).FirstOrDefault();

                if(obj != null)
                {
                    if (ProcessCheckIn(obj))
                    {
                        txtID.Text = "";                        
                    }
                }
                else
                {
                    MessageBox.Show(String.Format("ID {0} not found!", id));
                }

            }
            else
            {
                MessageBox.Show(String.Format("ID {0} not found!", id));
            }
     
        }
    }

    public class GuestModel
    {
        public DateTime CheckInDate { get; set; }
        public String GuestId { get; set; }
        public String GuestName { get; set; }

        public String CheckInDateText
        {
            get
            {
                var TIME_FORMAT = "HH:mm:ss";
                return CheckInDate.ToString(TIME_FORMAT);
            }
        }

        public GuestModel() { }

        public GuestModel(DateTime timeStamp, String guestId, String guestName)
        {
            CheckInDate = timeStamp;
            GuestId = guestId;
            GuestName = guestName;
        }
    }
}
