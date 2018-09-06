using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using System.Windows.Forms;
using PylonC.NETSupportLibrary;
using PylonLiveView;
using System.IO;
using System.Net;
using System.Threading;
using System.Runtime.InteropServices;
using Euresys.Open_eVision_1_2;
using PylonLiveView;
using System.Reflection;
using System.Diagnostics;
using MainSpace.PCI1245;
using Advantech.Motion;
using MainSpace.CustomPubClass;
using System.IO.Ports;


namespace PylonLiveView
{
    /* The main window. */
    public partial class MainForm : Form
    {
        [DllImport("kernel32")]
        public static extern int GetPrivateProfileString(string section, string key,
            string def, StringBuilder retVal, int size, string filePath);
        private static PCI1245_E m_form_movecontrol = new PCI1245_E();
        private Bitmap m_bitmap = null; /* The bitmap is used for displaying the image. */
        int m_current_num = 0; //��¼��ǰ��õ�ͼƬ��ţ�һ�Ų��������
        //��־λ
        //private bool m_ScanAuthorized = false; //������ҵ
        private bool m_inScanFunction = false; //busy��־
        //private int m_status_MachineMoveFinished = 0;   //��λ��ɨ������������ɱ�־�����=1��Ȼ���ֶ���0��
        //private bool m_tag_inCalibrate = false; //�Ƿ�����У׼��
        private bool m_tag_CalibrateOK = false; //У���Ƿ���ɣ���t��Ƭ���ܮ����uƷ��Ƭ
        private bool m_tag_InCheckAllDecodeFinished = false;  //�Ƿ��ڶ�����BLOCK���� decode finish check
        private bool m_tag_DBQueryFinished = false;   //��⵽������Ƿ���ɲ�ѯ����ı���ײ���λ��
        private bool m_tag_ShifttoStage2Checked = false;  //����ڶ���λ��ֻ��Ҫ���һ�α�־

        //�ۼ�mark��У׼�������������5��У׼ʧ�ܣ���ʾУ׼ʧ�ܣ���У׼ƫ��ֵ����Ϊ0,��������
        private int m_times_duplicateCalibrate = 0;

        //��Ȧ״̬
        private bool m_coilstatus_workmode = false;          //B00001      ��ҵ����(0:ɨ��ģʽ  1:ͨ��ģʽ)
        private bool m_coilstatus_EmergenceError = false;       //B00002     ����ֹͣ(0:����  1:����ֹͣ) 
        private bool m_coilstatus_Led = false;                  //B00004     ��Դ״̬(0:OFF   1:ON)
        private bool m_coilstatus_CCDTrigger = false;           //B00005     ��ҵ����(1:����) 
        private bool m_coilstatus_FixScan = false;              //B00006     ���벹ɨ(0:PASS   1:��Ҫ��ɨ) 
        //private bool m_coilstatus_SheetBarcodeScan = false;     //B00007     ��������ɨ��(0:FAIL   1:ɨ��ɹ�) 
        private bool m_coilstatus_Stage2Arrived = false;        //B00008     �ذ嵽����ԣ���ʼMARKУ׼
        private bool m_coilstatus_ShiftToStage2 = false;        //B00011     �ذ徭��ɨ������ڶ���λ
        private bool m_coilstatus_ArrivedScanPos = false;       //

        private List<BitmapInfo> m_list_bmpReceived;
        private EventWaitHandle m_wait_picReceived = new EventWaitHandle(false, EventResetMode.AutoReset);  //�˶���λ���ȴ���Ʒ������ϣ�

        private ScanbarcodeInfo m_barcodeinfo_preScan = new ScanbarcodeInfo();      //��һ��λɨ�赽����ʱ�洢(�����ͣ�Ļ���Ҫ���)
        private ScanbarcodeInfo m_barcodeinfo_CurrentUse = new ScanbarcodeInfo();    //����ڶ���λ��������ʽ����  
        //private string m_barcode_preScan = "";        //��һ��λɨ�赽����ʱ�洢(�����ͣ�Ļ���Ҫ���)
        //private List<int> m_listNGPosition_preScan = new List<int>();
        //private string m_barcode_CurrentUse = "";     //����ڶ���λ��������ʽ����
        //private List<int> m_listNGPosition_CurrentUse = new List<int>();
        private int m_count_BoardIn = 0;     //��������ۼƣ� ����ͳ��BOX�ڲ��Ƿ��а�

        private Thread thd_DeleteLog = null, thd_DeleteLog1 = null, thd_DeleteLog2 = null, thd_DeleteLog3 = null;
        MyFunctions myfunc = new MyFunctions();
        Modbus m_modbus;
        DBQuery m_DBQuery = new DBQuery();

        OBJ_DWGDirect m_obj_dwg;
        //ʵ�ʳߴ�����ͼ�������
        double m_ratio_Width = 1.00;
        double m_ratio_Height = 1.00;
        //ͼֽ�����м���ĵ�������ϵ�ο�ԭ��
        float refOrg_x = 0.00F;
        float reforg_y = 0.00F;
        bool m_initOK = true;  //�����жϹ��캯���Ƿ�ִ�гɹ���������ɹ�������
        /* Set up the controls and events to be used and update the device list. */
        public MainForm()
        {
            try
            {
                InitializeComponent();
                myfunc.ReadGlobalInfoFromTBS();
                openScanPort();
                updatetestinfo();
                //Thread t_welcome = new Thread(new ThreadStart(
                //    delegate
                //    {
                //BeginBeginInvoke(new Action(() =>
                //{
                Welcome w = new Welcome();
                if (w.ShowDialog() != DialogResult.OK)
                {
                    m_initOK = false;
                    System.Environment.Exit(0);
                }
                //}));
                //    }));
                //t_welcome.IsBackground = true;
                //t_welcome.Start();
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyFileVersionAttribute), false);
                if (attributes.Length > 0)
                {
                    tsslbl_ver.Text = "ܛ���汾��" + ((AssemblyFileVersionAttribute)attributes[0]).Version;
                }
                logWR.checkLogfileExist();

                //initImageProvider();   //fotest ltt
                baslerCCD1.InitCCDCamera();
                baslerCCD1.event_bmpReceive += new Basler.BaslerCCD.dele_bmpReceived(baslerCCD_event_bmpReceive);

                m_form_movecontrol.Parent = tabPage_1020;
                //ʹ�þ�����ʽ����
                Application.VisualStyleState = System.Windows.Forms.VisualStyles.VisualStyleState.NoneEnabled;

                m_obj_dwg = new OBJ_DWGDirect();
                m_obj_dwg.Dock = DockStyle.Fill;
                m_obj_dwg.eve_fileLoaded += new dele_CADFileLoaded(m_obj_dwg_eve_fileLoaded);
                //m_obj_dwg.eve_sendTPMessage += new dele_SendMessage(m_obj_dwg_eve_sendTPMessage);
                m_obj_dwg.eve_sendReFPoint += new dele_SendRefPoint(m_obj_dwg_eve_sendReFPoint);
                m_obj_dwg.eve_returnMechicalOrgPoint += new dele_SendMessage(returnMachicalOrgPoint);
                m_obj_dwg.eve_returnRefPoint += new dele_SendMessage(returnreferecePoint);
                m_obj_dwg.eve_sendFixMotion += new dele_SendFixMotion(m_obj_dwg_eve_sendFixMotion);
                m_obj_dwg.eve_sendCalPosition += new dele_SendFixMotion(m_obj_dwg_eve_sendCalPosition);
                m_obj_dwg.Parent = tabPage_CADView;
                this.MouseWheel += new MouseEventHandler(m_obj_dwg.OBJ_DWGDirect_MouseWheel);
                this.FormClosing += new FormClosingEventHandler(m_obj_dwg.On_control_Closing);

                m_form_movecontrol.eve_SheetBarcodeScan += new EventHandler(m_form_movecontrol_eve_SheetBarcodeScan);
                m_form_movecontrol.eve_BoardArrived += new EventHandler(m_form_movecontrol_eve_BoardArrived);
                m_form_movecontrol.eve_EmergeceStop += new EventHandler(m_form_movecontrol_eve_EmergeceStop);
                m_form_movecontrol.eve_EmergenceRelease += new EventHandler(m_form_movecontrol_eve_EmergenceRelease);
                m_form_movecontrol.eve_MotionMsg += new dele_MotionMsg(m_form_movecontrol_eve_MotionMsg);
                m_form_movecontrol.eve_SofetyDoor += new EventHandler(m_form_movecontrol_eve_SofetyDoor);
            }
            catch (Exception e)
            {
                throw new Exception(e.ToString());
            }
        }

        /// <summary>
        /// ��ȫ��
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void m_form_movecontrol_eve_SofetyDoor(object sender, EventArgs e)
        {
            try
            {
                this.Invoke(new Action(() =>
                {
                    if (!GlobalVar.gl_safetyDoor_Front && GlobalVar.gl_usermode == 0)
                    {
                        WorkPermitted(false); //��ȫ��δ�أ���ֹ��ҵ
                        button_status.Text = "ǰ��δ��";
                    }
                    else
                    {
                        button_status.Text = "���C��";
                    }
                }));
            }
            catch { }
        }

        /// <summary>
        /// ���յ�ͼƬ������(ֻ�����Զ�����)
        /// </summary>
        /// <param name="bmp">ͼƬ</param>
        /// <param name="isManualPic">�Ƿ�Ϊ�ֶ�����</param>
        public void baslerCCD_event_bmpReceive(ImageProvider.Image image)
        {
            try
            {
                if (image != null)
                {
                    if (GlobalVar.m_ScanAuthorized && m_tag_CalibrateOK)
                    {
                        m_wait_picReceived.Set();
                    }
                    /* Check if the image is compatible with the currently used bitmap. */
                    if (BitmapFactory.IsCompatible(m_bitmap, image.Width, image.Height, image.Color))
                    {
                        /* Update the bitmap with the image data. */
                        BitmapFactory.UpdateBitmap(m_bitmap, image.Buffer, image.Width, image.Height, image.Color);
                        /* To show the new image, request the display control to update itself. */
                        pictureBox_capture.Refresh();
                    }
                    else /* A new bitmap is required. */
                    {
                        BitmapFactory.CreateBitmap(out m_bitmap, image.Width, image.Height, image.Color);
                        BitmapFactory.UpdateBitmap(m_bitmap, image.Buffer, image.Width, image.Height, image.Color);
                        /* We have to dispose the bitmap after assigning the new one to the display control. */
                        Bitmap bitmap = pictureBox_capture.Image as Bitmap;
                        pictureBox_capture.Image = m_bitmap;
                        if (bitmap != null)
                        {
                            /* Dispose the bitmap. */
                            bitmap.Dispose();
                        }
                    }

                    m_bitmap.RotateFlip(RotateFlipType.Rotate90FlipNone);
                    if (m_bitmap_calibrate_REF == null) { m_bitmap_calibrate_REF = (Bitmap)m_bitmap.Clone(); return; }
                    if (m_bitmap_calibrate_END == null) { m_bitmap_calibrate_END = (Bitmap)m_bitmap.Clone(); return; }
                    if (GlobalVar.m_ScanAuthorized && m_tag_CalibrateOK)
                    {
                        BitmapInfo bi = new BitmapInfo();
                        bi.FlowID = GlobalVar.gl_CurrentFlowID;
                        bi.bitmap = (Bitmap)m_bitmap.Clone();
                        bi.num = m_current_num;
                        m_list_bmpReceived.Add(bi);
                    }
                }
            }
            catch { }
        }

        private void NewPartNoLoad()
        {
            this.Invoke(new Action(() =>
            {
                lbl_refMarkX.Text = "";
                lbl_refMarkY.Text = "";
            }));
            // string LinkType = GlobalVar.gl_LinkType.ToString();  //PROX or MIC
            //string iniFilePath = Application.StartupPath + "\\" + GlobalVar.gl_ProductModel + "\\" + GlobalVar.gl_LinkType + "\\" + GlobalVar.gl_ProductModel.ToUpper() + "_MAPPING.INI";
            string iniFilePath = Application.StartupPath + "\\" + GlobalVar.gl_ProductModel + "\\" + GlobalVar.gl_LinkType + "\\" + GlobalVar.gl_ProductModel.ToUpper() + "_MAPPING.INI";
            if (!File.Exists(iniFilePath))
            {
                MessageBox.Show("�]���ҵ�����·����ӳ�������ęn�������P�]��Ո�_�J��");
                DialogResult = DialogResult.Cancel;
                //Application.Exit();
                return;
            }
            GlobalVar.gl_matchFileName = GlobalVar.gl_SpecialPath + "\\" + GlobalVar.gl_ProductModel.ToUpper() + ".MCH";
            GlobalVar.gl_matchFileName = Application.StartupPath + "\\" + GlobalVar.gl_ProductModel + "\\" + GlobalVar.gl_LinkType + "\\" + GlobalVar.gl_ProductModel.ToUpper() + ".MCH";//�޸�Ϊ��ȡ����ģʽ
            if (!File.Exists(GlobalVar.gl_matchFileName))
            {
                MessageBox.Show("�]���ҵ�����·������ƷĿMCH�ļ���" + GlobalVar.gl_matchFileName + "�������P�]��Ո�_�J!");
                //MessageBox.Show("�]���ҵ�����·������ƷĿMCH�ļ���" + GlobalVar.gl_matchFileName + "�������P�]��Ո�_�J!");
                DialogResult = DialogResult.Cancel;
                return;
            }
            myfunc.ReadRefPointInfoFromTBS();
            checkConfigFolderExist();
            setRefPointValue(GlobalVar.gl_Ref_Point_Axis.Pos_X.ToString("0.000"),
                GlobalVar.gl_Ref_Point_Axis.Pos_Y.ToString("0.00"));
            m_form_movecontrol.AutoHomeSearch_Manual();
            //�Զ�����CAD�ĵ�
            string CADFile = Application.StartupPath + "\\" + GlobalVar.gl_ProductModel + "\\" + GlobalVar.gl_LinkType + "\\" + GlobalVar.gl_ProductModel.ToUpper() + ".DWG";
            //string CADFile = GlobalVar.gl_SpecialPath  + "\\" + GlobalVar.gl_ProductModel.ToUpper() + ".DWG";
            if (!File.Exists(CADFile))
            {
                MessageBox.Show("�]���ҵ�����·���µ�ǰƷĿ��CAD�ĵ�����ʹ���ֶ����룬Ո�_�J��");
            }
            else
            {
                m_obj_dwg.LoadCADFile(CADFile, 1);
            }
            this.Invoke(new Action(() =>
            {
                lbl_refMarkX.Text = GlobalVar.gl_point_CalPosRef.Pos_X.ToString();
                lbl_refMarkY.Text = GlobalVar.gl_point_CalPosRef.Pos_Y.ToString();
            }));
        }
        //�������
        private void NewPartNoNetLoad()
        {
            myfunc.LoadShare();
            this.Invoke(new Action(() =>
            {
                lbl_refMarkX.Text = "";
                lbl_refMarkY.Text = "";
            }));
            string LinkType = GlobalVar.gl_LinkType.ToString();  //PROX or MIC
            string iniFilePath = GlobalVar.gl_netPath + GlobalVar.gl_appName + "\\" + GlobalVar.gl_ProductModel + "\\" + LinkType + "\\" + GlobalVar.gl_ProductModel.ToUpper() + "_MAPPING.INI";
            if (!File.Exists(iniFilePath))
            {
                MessageBox.Show("�]���ҵ�����·����ӳ�������ęn�������P�]��Ո�_�J��");
                DialogResult = DialogResult.Cancel;
                //Application.Exit();
                return;
            }
            GlobalVar.gl_matchFileName = GlobalVar.gl_netPath + GlobalVar.gl_appName + "\\" + GlobalVar.gl_ProductModel + "\\" + LinkType + "\\" + GlobalVar.gl_ProductModel.ToUpper() + ".MCH";
            GlobalVar.gl_matchFileName = Application.StartupPath + "\\" + GlobalVar.gl_ProductModel + "\\" + GlobalVar.gl_LinkType + "\\" + GlobalVar.gl_ProductModel.ToUpper() + ".MCH";//�޸�Ϊ��ȡ����ģʽ
            if (!File.Exists(GlobalVar.gl_matchFileName))
            {
                MessageBox.Show("�]���ҵ�����·������ƷĿMCH�ļ���" + GlobalVar.gl_matchFileName + "�������P�]��Ո�_�J!");
                //MessageBox.Show("�]���ҵ�����·������ƷĿMCH�ļ���" + GlobalVar.gl_matchFileName + "�������P�]��Ո�_�J!");
                DialogResult = DialogResult.Cancel;
                return;
            }
            myfunc.ReadRefPointInfoFromTBS();
            checkConfigFolderExist();
            setRefPointValue(GlobalVar.gl_Ref_Point_Axis.Pos_X.ToString("0.000"),
                GlobalVar.gl_Ref_Point_Axis.Pos_Y.ToString("0.00"));
            m_form_movecontrol.AutoHomeSearch_Manual();
            //�Զ�����CAD�ĵ�
            string CADFile = GlobalVar.gl_netPath + GlobalVar.gl_appName + "\\" + GlobalVar.gl_ProductModel + "\\" + LinkType + "\\" + GlobalVar.gl_ProductModel.ToUpper() + ".DWG";
            if (!File.Exists(CADFile))
            {
                MessageBox.Show("�]���ҵ���ǰƷĿ��CAD�ĵ�����ʹ���ֶ����룬Ո�_�J��");
            }
            else
            {
                m_obj_dwg.LoadCADFile(CADFile, 1);
            }
            this.Invoke(new Action(() =>
            {
                lbl_refMarkX.Text = GlobalVar.gl_point_CalPosRef.Pos_X.ToString();
                lbl_refMarkY.Text = GlobalVar.gl_point_CalPosRef.Pos_Y.ToString();
            }));
        }

        //��ȡͨ���ĵ�--2017.10.07
        private bool CommenConfigLoad()
        {
            bool result = false;
            myfunc.LoadShare();
            this.Invoke(new Action(() =>
            {
                lbl_refMarkX.Text = "";
                lbl_refMarkY.Text = "";
            }));
            string iniFilePath = GlobalVar.gl_netPath + GlobalVar.gl_appName + "\\" + GlobalVar.gl_ProductModel + "\\" + GlobalVar.gl_ProductModel.ToUpper() + "_MAPPING.INI";
            if (!File.Exists(iniFilePath))
            {
                //MessageBox.Show("�]���ҵ�����·����ͨ��ӳ�������ęn�������P�]��Ո�_�J��");
                //DialogResult = DialogResult.Cancel;
                //Application.Exit();
                return result;
            }
            //GlobalVar.gl_matchFileName = GlobalVar.gl_netPath + GlobalVar.gl_appName + "\\" + GlobalVar.gl_ProductModel + "\\" + LinkType + "\\" + GlobalVar.gl_ProductModel.ToUpper() + ".MCH";
            GlobalVar.gl_matchFileName = Application.StartupPath + "\\" + GlobalVar.gl_ProductModel + "\\" + GlobalVar.gl_LinkType + "\\" + GlobalVar.gl_ProductModel.ToUpper() + ".MCH";//�޸�Ϊ��ȡ����ģʽ
            if (!File.Exists(GlobalVar.gl_matchFileName))
            {
                //MessageBox.Show("�]���ҵ�����·������ƷĿMCH�ļ���" + GlobalVar.gl_matchFileName + "�������P�]��Ո�_�J!");
                //MessageBox.Show("�]���ҵ�����·������ƷĿMCH�ļ���" + GlobalVar.gl_matchFileName + "�������P�]��Ո�_�J!");
                //DialogResult = DialogResult.Cancel;
                return result;
            }
            myfunc.ReadRefPointInfoFromTBS();
            checkConfigFolderExist();
            setRefPointValue(GlobalVar.gl_Ref_Point_Axis.Pos_X.ToString("0.000"),
                GlobalVar.gl_Ref_Point_Axis.Pos_Y.ToString("0.00"));
            m_form_movecontrol.AutoHomeSearch_Manual();
            //�Զ�����CAD�ĵ�
            string CADFile = GlobalVar.gl_netPath + GlobalVar.gl_appName + "\\" + GlobalVar.gl_ProductModel + "\\" + GlobalVar.gl_ProductModel.ToUpper() + ".DWG";
            if (!File.Exists(CADFile))
            {
                //MessageBox.Show("�]���ҵ���ǰƷĿ��ͨ��CAD�ĵ�����ʹ���ֶ����룬Ո�_�J��");
                DialogResult = DialogResult.Cancel;
                return result;
            }
            else
            {
                m_obj_dwg.LoadCADFile(CADFile, 1);
                result = true;
            }
            this.Invoke(new Action(() =>
            {
                lbl_refMarkX.Text = GlobalVar.gl_point_CalPosRef.Pos_X.ToString();
                lbl_refMarkY.Text = GlobalVar.gl_point_CalPosRef.Pos_Y.ToString();
            }));
            return result;
        }

        //���ƷĿ�����ļ����Ƿ���ڣ�����������򴴽�
        private void checkConfigFolderExist()
        {
            if (!Directory.Exists(Application.StartupPath + "\\" + GlobalVar.gl_ProductModel))
            {
                Directory.CreateDirectory(Application.StartupPath + "\\" + GlobalVar.gl_ProductModel);
            }
        }
        //��ȡ�豸���
        private void getDeviecId()
        {
            string strPorductTypeINI = GlobalVar.gl_strTargetPath + "\\" + GlobalVar.gl_iniTBS_FileName;
            StringBuilder str_tmp = new StringBuilder(100);
            MyFunctions.GetPrivateProfileString(GlobalVar.gl_inisection_Global, GlobalVar.gl_iniKey_strDeviceID, "", str_tmp, 500, strPorductTypeINI);
            GlobalVar.gl_DeviceID = str_tmp.ToString();
            txtbox_DeviceID.Text = GlobalVar.gl_DeviceID;
            //Z�� 2017.12.18
            MyFunctions.GetPrivateProfileString(GlobalVar.gl_iniSection_AxisZRef, GlobalVar.gl_inikey_lastLinkType, "", str_tmp, 50, Application.StartupPath + "\\config.ini");
            switch (str_tmp.ToString())
            {
                case "1":
                    GlobalVar.gl_LastLinkType = LinkType.PROX;
                    break;
                case "2":
                    GlobalVar.gl_LastLinkType = LinkType.MIC;
                    break;
                case "3":
                    GlobalVar.gl_LastLinkType = LinkType.BARCODE;
                    break;
                case "4":
                    GlobalVar.gl_LastLinkType = LinkType.IC;
                    break;
                default:
                    GlobalVar.gl_LastLinkType = LinkType.DEFAULT;
                    break;
            }
            //Z��ο���
            MyFunctions.GetPrivateProfileString(GlobalVar.gl_iniSection_AxisZRef, GlobalVar.gl_LinkType.ToString(), "", str_tmp, 50, strPorductTypeINI);
            GlobalVar.gl_dAxisZRef = str_tmp.ToString() == "" ? 0.0 : Convert.ToDouble(str_tmp.ToString());
            m_form_movecontrol.ShowAixsZRef(GlobalVar.gl_dAxisZRef);
        }
        //��ȡ�����ַ
        private void getConfigModel()
        {
            string strPorductTypeINI = GlobalVar.gl_strTargetPath + "\\" + GlobalVar.gl_iniTBS_FileName;
            StringBuilder str_tmp = new StringBuilder(100);
            MyFunctions.GetPrivateProfileString(GlobalVar.gl_inisection_Global, GlobalVar.gl_inikey_specialPath, "", str_tmp, 500, strPorductTypeINI);
            GlobalVar.gl_SpecialPath = str_tmp.ToString();
            if (GlobalVar.gl_SpecialPath.Trim() != "")
            {
                GlobalVar.gl_AutoLoadType = false;
            }
        }
        private void MainForm_Load(object sender, EventArgs e)
        {
            try
            {
                GlobalVar.gl_IntPtr_MainWindow = this.Handle;
                m_list_bmpReceived = new List<BitmapInfo>();
                //timer_alarm.Start();
                //timer1.Start();
                tabControl1.SelectedIndex = 1;
                TipPointShow.Width = 1;
                rdb_NetLoading.Checked = true;
                if (!Directory.Exists(GlobalVar.gl_Directory_savePath))
                {
                    Directory.CreateDirectory(GlobalVar.gl_Directory_savePath);
                }
                try
                {
                    string sql = "SELECT distinct SubName FROM [BASEDATA].[dbo].[BasCheckPart]" +
                           " where (ClassName = 'MIC' or ClassName = 'PROX') and SubName <> 'TEST' and SubName <> ''";
                    DataTable dt1 = m_DBQuery.get_database_BaseData(sql);
                    if (dt1 != null)
                    {
                        for (int n = 0; n < dt1.Rows.Count; n++)
                        {
                            string str = dt1.Rows[0 + n]["SubName"].ToString();
                            GlobalVar.listProductType.Add(str);
                        }
                    }
                }
                catch { }
            }
            catch (Exception ex)
            {
                MessageBox.Show("�����w���d���� : " + ex.ToString());
            }
            getDeviecId();
            getConfigModel();
        }

        //ɾ����־
        private void ThdDeleteLog()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(2000);
                    string log = Application.StartupPath + "\\LOG\\";
                    if (!Directory.Exists(log)) continue;
                    MyFunctions.DeleteLogFunc(GlobalVar.gl_NGPicsSavePath, 15);
                    Thread.Sleep(8 * 60 * 60 * 1000); //8Сʱ
                }
                catch { }
            }
        }
        private void ThdDeleteLog1()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(2000);
                    if (!Directory.Exists(GlobalVar.gl_path_FileBackUp)) continue;
                    MyFunctions.DeleteLogFunc(GlobalVar.gl_path_FileBackUp, 15);
                    Thread.Sleep(8 * 60 * 60 * 1000); //8Сʱ
                }
                catch { }
            }
        }
        private void ThdDeleteLog2()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(2000);
                    if (!Directory.Exists(GlobalVar.gl_PicsSavePath)) continue;
                    MyFunctions.DeleteLogFunc(GlobalVar.gl_PicsSavePath, 15);
                    Thread.Sleep(8 * 60 * 60 * 1000); //8Сʱ
                }
                catch { }
            }
        }
        private void ThdDeleteLog3()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(2000);
                    if (!Directory.Exists(GlobalVar.gl_NGPicsSavePath)) continue;
                    MyFunctions.DeleteLogFunc(GlobalVar.gl_NGPicsSavePath, 15);
                    Thread.Sleep(8 * 60 * 60 * 1000); //8Сʱ
                }
                catch { }
            }
        }
        private void StartThdDelLog()
        {
            if (thd_DeleteLog == null)
            {
                thd_DeleteLog = new Thread(ThdDeleteLog);
                thd_DeleteLog.IsBackground = true;
                thd_DeleteLog.Name = "ɾ����־";
                thd_DeleteLog.Start();
            }
            if (thd_DeleteLog1 == null)
            {
                thd_DeleteLog1 = new Thread(ThdDeleteLog1);
                thd_DeleteLog1.IsBackground = true;
                thd_DeleteLog1.Name = "ɾ����־1";
                thd_DeleteLog1.Start();
            }
            if (thd_DeleteLog2 == null)
            {
                thd_DeleteLog2 = new Thread(ThdDeleteLog2);
                thd_DeleteLog2.IsBackground = true;
                thd_DeleteLog2.Name = "ɾ����־1";
                thd_DeleteLog2.Start();
            }
            if (thd_DeleteLog3 == null)
            {
                thd_DeleteLog3 = new Thread(ThdDeleteLog3);
                thd_DeleteLog3.IsBackground = true;
                thd_DeleteLog3.Name = "ɾ����־1";
                thd_DeleteLog3.Start();
            }
        }
        private void MainForm_Shown(object sender, EventArgs e)
        {
            GlobalVar.gl_strPCName = System.Net.Dns.GetHostName();
            if (!m_initOK) { Application.Exit(); }
            StartThdDelLog();
            toolStripButton_LinkType.Text = "������ҵ����:[" + GlobalVar.gl_LinkType.ToString() + "]";
            initTestInfo();
            m_ratio_Width = tabPage_mainview.Width * 1.00 / GlobalVar.gl_workArea_width;
            m_ratio_Height = tabPage_mainview.Height * 1.00 / GlobalVar.gl_workArea_height;
            //if (updateDeviceListTimer != null) //ltt
            //{
            //    updateDeviceListTimer.Enabled = true;
            //}
            //�˶����Ƴ�ʼ��
            GlobalVar.gl_Board1245EInit = m_form_movecontrol.OpenBoardAndInit();
            if (!GlobalVar.gl_Board1245EInit)
            {
                MessageBox.Show("�˶����ƿ���ʼ��ʧ�ܣ���ȷ�ϣ�", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            m_form_movecontrol.ServerON_Axis_X();
            m_form_movecontrol.ServerON_Axis_Y();
            m_form_movecontrol.AddAxisIntoGroup(0);
            m_form_movecontrol.AddAxisIntoGroup(1);
            m_form_movecontrol.ALLIOInit();
            m_form_movecontrol.Enabled = false;
            updateLedLightStatus(0);
        }

        #region �˶����ƿ��ص�����
        //����ɨ��λ�ã���ʼɨ��
        void m_form_movecontrol_eve_SheetBarcodeScan(object sender, EventArgs e)
        {
            if ((!GlobalVar.m_ScanAuthorized) || GlobalVar.gl_inEmergence) return;
            if (!GlobalVar.gl_safetyDoor_Front && GlobalVar.gl_usermode == 0) return;
            //�����ʷprescan����
            m_barcodeinfo_preScan.Clear();
            m_count_BoardIn++;
            updateLedLightStatus(1);
            ThreadPool.QueueUserWorkItem(StartScanPanelBarcode);
        }

        //ֹͣ��λ���
        void m_form_movecontrol_eve_EmergenceRelease(object sender, EventArgs e)
        {
            updateLedLightStatus(0);
            m_form_movecontrol.Stage1ZaibanPass();
            m_form_movecontrol.Stage2ZaibanPass();
            m_form_movecontrol.AutoHomeSearch_Manual();
            //m_form_movecontrol.LedLight_Red(1);
            //m_form_movecontrol.LedLight_Beep(1);
            BeginInvoke(new Action(() =>
            {
                timer_alarm.Enabled = false;
                button_alarm.BackColor = Color.Gray;
            }));
            clearTags();
            m_manualCycleReset = true;
            Thread.Sleep(800);
            OneCircleReset();

            //m_form_movecontrol.InitDevice();
            //m_obj_dwg.setRefPointValue(GlobalVar.gl_Ref_Point_Axis.Pos_X.ToString("0.000"),
            //    GlobalVar.gl_Ref_Point_Axis.Pos_Y.ToString("0.00"));
        }

        void m_form_movecontrol_eve_EmergeceStop(object sender, EventArgs e)
        {
            BeginInvoke(new Action(() =>
            {
                timer_alarm.Enabled = true;
                button_sheetSNInfo.BackColor = Color.DarkGray;
                button_sheetSNInfo.Text = "";
                updateLedLightStatus(2);
            }));
            EmergenceReset();
            if (thread_calibrate != null)
            {
                if (thread_calibrate.IsAlive)
                {
                    thread_calibrate.Abort();
                }
            }
            //PCI1020.PCI1020_Reset(m_form_movecontrol.hDevice);
        }

        //�ذ嵽λ
        void m_form_movecontrol_eve_BoardArrived(object sender, EventArgs e)
        {
            if ((!GlobalVar.m_ScanAuthorized) || GlobalVar.gl_inEmergence) return;
            if (!GlobalVar.gl_safetyDoor_Front && GlobalVar.gl_usermode == 0) return;
            try
            {
                addLogStr(DateTime.Now.ToString("yyyyMMdd HH:mm:ss") + ": ��һ��λ�d�嵽λ���_ʼ����SHEET�l�a��");
                //richTextBox_SingleShow.AppendText(DateTime.Now.ToString("yyyyMMdd HH:mm:ss") + ":\t��һ��λ�d�嵽λ���_ʼ����SHEET�l�a��");            
                AddShowLog(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + "\r\n��һ��λ�d�嵽λ���_ʼ����SHEET�l�a��", Color.Green);
                updateLedLightStatus(1);
                m_inScanFunction = true;
                m_barcodeinfo_CurrentUse.Clone(m_barcodeinfo_preScan);
                m_barcodeinfo_preScan.Clear();
                BeginInvoke(new Action(() =>
                {
                    if (m_barcodeinfo_CurrentUse.barcode.Trim().Length == 0)
                    {
                        updateLedLightStatus(2);
                        button_sheetSNInfo.BackColor = Color.DarkRed;
                        button_sheetSNInfo.Text = "SHEET�l�a����ʧ��";
                        ShowPsdErrForm sp = new ShowPsdErrForm("SHEET�l�a����ʧ��,Ո�������I!", false);
                        sp.ShowDialog();
                        updateLedLightStatus(1);
                        m_barcodeinfo_CurrentUse.Clear();
                        m_form_movecontrol.Stage2ZaibanPass();
                        AddShowLog(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + "\r\nSHEET�l�a����ʧ��,Ո�������I!", Color.Red);
                        m_count_BoardIn--;
                        updateLedLightStatus(0);
                        clearTags();
                    }
                    else if (!m_barcodeinfo_CurrentUse.LotResult)
                    {
                        updateLedLightStatus(2);
                        ShowPsdErrForm sp = new ShowPsdErrForm(m_barcodeinfo_CurrentUse.ErrMsg_Lot, true);
                        sp.ShowDialog();
                        m_form_movecontrol.Stage2ZaibanPass();
                        //richTextBox_SingleShow.AppendText(DateTime.Now.ToString("yyyyMMdd HH:mm:ss") + "\t�����ذ��˳�");
                        AddShowLog(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + "\r\n�����ذ��˳�", Color.Green);
                        m_count_BoardIn--;
                        updateLedLightStatus(0);
                        clearTags();
                    }
                    else
                    {
                        if (GlobalVar.gl_LinkType == LinkType.BARCODE)// sheet������洢���� [10/18/2017 617004]
                        {
                            if (!CheckShtBarcode(m_barcodeinfo_CurrentUse.barcode.Trim()))
                            {
                                updateLedLightStatus(2);
                                button_sheetSNInfo.BackColor = Color.DarkRed;
                                button_sheetSNInfo.Text = "SHEET�l�a�����쳣";
                                updateLedLightStatus(1);
                                m_barcodeinfo_CurrentUse.Clear();
                                m_form_movecontrol.Stage2ZaibanPass();
                                ShowPsdErrForm sp = new ShowPsdErrForm("SHEET�l�a����ʧ��,Ո�������I!", false);
                                sp.ShowDialog();
                                AddShowLog(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + "\r\nSHEET�l�a�����쳣,Ո�������I!", Color.Red);
                                m_count_BoardIn--;
                                updateLedLightStatus(0);
                                clearTags();
                            }
                            else
                            {
                                button_sheetSNInfo.BackColor = Color.Green;
                                button_sheetSNInfo.Text = m_barcodeinfo_CurrentUse.barcode;
                                startScanFunction();
                            }

                        }
                        else if (GlobalVar.gl_LinkType == LinkType.IC)
                        {
                            if (!CheckShtBarcode_IC(m_barcodeinfo_CurrentUse.barcode.Trim()))
                            {
                                updateLedLightStatus(2);
                                button_sheetSNInfo.BackColor = Color.DarkRed;
                                button_sheetSNInfo.Text = "SHEET�l�a����ʧ��";
                                ShowPsdErrForm sp = new ShowPsdErrForm("SHEET�l�a��IC��������,Ո�������I!", false);
                                sp.ShowDialog();
                                updateLedLightStatus(1);
                                m_barcodeinfo_CurrentUse.Clear();
                                m_form_movecontrol.Stage2ZaibanPass();
                                AddShowLog(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + "\r\nSHEET�l�a��IC��������,Ո�������I!", Color.Red);
                                m_count_BoardIn--;
                                updateLedLightStatus(0);
                                clearTags();
                            }
                            else
                            {
                                button_sheetSNInfo.BackColor = Color.Green;
                                button_sheetSNInfo.Text = m_barcodeinfo_CurrentUse.barcode;
                                startScanFunction();
                            }
                        }
                        else
                        {
                            button_sheetSNInfo.BackColor = Color.Green;
                            button_sheetSNInfo.Text = m_barcodeinfo_CurrentUse.barcode;
                            ////���ذ��˶��쳣�����ж�  --20171011 lqz
                            //while (GlobalVar.gl_bUseLargeBoardSize && m_count_BoardIn != 1)
                            //{
                            //    Thread.Sleep(50);//���ʹ�ô�岢�һ������ذ�����Ψһ���������ź�
                            //}
                            startScanFunction();
                        }
                    }
                }));

            }
            catch (Exception ex)
            {
                logWR.appendNewLogMessage("��λ��ʼ�˶������쳣 m_form_movecontrol_eve_BoardArrived error:  \r\n " + ex.ToString());
            }
        }

        //�˶�����Ϣ����
        void m_form_movecontrol_eve_MotionMsg(string msg)
        {
            button_RunMsg.Invoke(new Action(() =>
            {
                button_RunMsg.Text = msg;
            }));
        }
        #endregion

        /// <summary>
        /// 0: YELLOW ON ,OTHERS OFF        ----- IDLE
        /// 1: GREEN ON ,OTHERS OFF         ----- BUSY
        /// 2: RED ON , BEEP ON, OTHERS OFF ----- ERROR
        /// </summary>
        /// <param name="Red"></param>
        private void updateLedLightStatus(uint type)
        {
            switch (type)
            {
                case 0:
                default:
                    m_form_movecontrol.LedLight_Yellow(1);
                    m_form_movecontrol.LedLight_Green(0);
                    m_form_movecontrol.LedLight_Red(0);
                    m_form_movecontrol.LedLight_Beep(0);
                    break;
                case 1:
                    m_form_movecontrol.LedLight_Yellow(0);
                    m_form_movecontrol.LedLight_Green(1);
                    m_form_movecontrol.LedLight_Red(0);
                    m_form_movecontrol.LedLight_Beep(0);
                    break;
                case 2:
                    m_form_movecontrol.LedLight_Red(1);
                    m_form_movecontrol.LedLight_Beep(1);
                    if (m_count_BoardIn > 0)
                    {
                        m_form_movecontrol.LedLight_Yellow(1);
                        m_form_movecontrol.LedLight_Green(0);
                    }
                    else
                    {
                        m_form_movecontrol.LedLight_Yellow(0);
                        m_form_movecontrol.LedLight_Green(0);
                    }
                    break;
            }
        }

        private void camConnectAutomatic() //ltt
        {
            //try
            //{
            //    if (deviceListView.Items.Count > 0)
            //    {
            //        ConnectWebCam();
            //        //ContinuousShot(); /* Start the grabbing of images until grabbing is stopped. */
            //    }
            //    /* Do not update device list while grabbing to avoid jitter because the GUI-Thread is blocked for a short time when enumerating. */
            //    updateDeviceListTimer.Stop();
            //    updateDeviceListTimer.Enabled = false;
            //    updateDeviceListTimer = null;
            //}
            //catch { }
        }

        private void updatetestinfo()
        {
            try
            {
                textBox_totalsheets.Text = GlobalVar.gl_testinfo_totalSheet.ToString();
                textBox_decodeNG.Text = GlobalVar.gl_testinfo_decodefailed.ToString();
                textBox_totalpcs.Text = GlobalVar.gl_testinfo_totalTest.ToString();
                if (GlobalVar.gl_testinfo_totalTest == 0)
                { textBox_decoderate.Text = "0.0"; }
                else
                {
                    textBox_decoderate.Text = ((GlobalVar.gl_testinfo_totalTest - GlobalVar.gl_testinfo_decodefailed) * 100.00 / GlobalVar.gl_testinfo_totalTest).ToString("0.00");
                }
                SaveTestInfo();
            }
            catch { }
        }

        //DWGDirect ģ�����CAD���
        void m_obj_dwg_eve_fileLoaded()
        {
            //try
            //{
            //    GlobalVar.gl_totalCount = 0;
            //    GlobalVar.gl_List_BlockInfo.Clear();
            //    tabPage_mainview.Controls.Clear();
            //    for (int i = 0; i < GlobalVar.gl_List_PointInfo.Count; i++)
            //    {
            //        DetailBlock bi = new DetailBlock();
            //        bi.Pos_X_CAD = Math.Abs(GlobalVar.gl_List_PointInfo[i].Pos_X); 
            //        bi.Pos_Y_CAD =  Math.Abs(GlobalVar.gl_List_PointInfo[i].Pos_Y );
            //        bi.Pos_Z_CAD = Math.Abs(GlobalVar.gl_List_PointInfo[i].Pos_Z ); 
            //        bi.m_PcsNo = GlobalVar.gl_List_PointInfo[i].PointNumber;
            //        bi.m_PcsNo_Mapping = GetMapNum(bi.m_PcsNo);
            //        bi.Location = newPointConvert(bi);
            //        bi.Width = GlobalVar.block_width;
            //        bi.Height = GlobalVar.block_heigt;
            //        bi.setPositionDisplay((Math.Abs(GlobalVar.gl_List_PointInfo[i].Pos_X - GlobalVar.gl_Ref_Point_CADPos.Pos_X).ToString("0.00"))
            //            , (Math.Abs(GlobalVar.gl_List_PointInfo[i].Pos_Y - GlobalVar.gl_Ref_Point_CADPos.Pos_Y)).ToString("0.00"));
            //        bi.Parent = tabPage_mainview;
            //        GlobalVar.gl_List_BlockInfo.Add(bi);
            //    }
            //    GlobalVar.gl_totalCount = GlobalVar.gl_List_BlockInfo.Count;
            //    //init_tabPage_mainview();
            //}
            //catch(Exception e)
            //{
            //    throw new Exception("��ʼ��gl_List_BlockInfo����" + e.ToString());
            //}
        }

        private int GetMapNum(int orgNum)
        {
            try
            {
                string iniFilePath = "";
                StringBuilder str_tmp = new StringBuilder(50);
                if (!GlobalVar.gl_AutoLoadType || textBox_LotNo.Text == "99999999999")
                    iniFilePath = Application.StartupPath + "\\" + GlobalVar.gl_ProductModel + "\\" + GlobalVar.gl_LinkType.ToString() + "\\" + GlobalVar.gl_ProductModel.ToUpper() + "_MAPPING.INI";
                else
                    iniFilePath = GlobalVar.gl_netPath + GlobalVar.gl_appName + "\\" + GlobalVar.gl_ProductModel + "\\" + GlobalVar.gl_LinkType.ToString() + "\\" + GlobalVar.gl_ProductModel.ToUpper() + "_MAPPING.INI";
                if (!File.Exists(iniFilePath))
                {
                    throw new Exception("�Ҳ���ӳ��λ�����Ùn�ļ���" + iniFilePath);
                }

                MyFunctions.GetPrivateProfileString(GlobalVar.gl_iniSection_mapping, orgNum.ToString(), "", str_tmp, 50, iniFilePath);
                orgNum = (str_tmp.ToString().Trim() == "" ? orgNum : Convert.ToInt32(str_tmp.ToString()));
            }
            catch
            {
                MessageBox.Show("ӳ��λ�����Ùn�xȡ���e��Ո�z�������ęn�Ƿ����_", "�e�`", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
            }
            return orgNum;
        }

        ////ʵ�ʿ����ؼ���ȵı�������ʾ��Ʒʱ��չ
        //float ratio_Width = 1.00F;
        //float ratio_Height = 1.00F;
        private Point newPointConvert(DetailBlock bi, int a)
        {
            Point p = new Point(0, 0);
            p.X = Convert.ToInt32(Math.Abs(GlobalVar.gl_point_ScrrenRefPoint.Pos_X - bi.Pos_X_CAD) * m_ratio_Width);
            p.Y = Convert.ToInt32(Math.Abs(GlobalVar.gl_point_ScrrenRefPoint.Pos_Y - bi.Pos_Y_CAD) * m_ratio_Height);
            #region    ���A51SENSOR block���࣬�ʵ�������ֵ
            if (a == 0)
                GlobalVar.firstBlockLocation = p.Y;
            if (p.Y > GlobalVar.firstBlockLocation && GlobalVar.firstBlockLocation != 0)
            {
                GlobalVar.firstBlockLocation = 0;
                GlobalVar.firstBlockCount = a - 1;
            }
            if (a > GlobalVar.firstBlockCount && GlobalVar.firstBlockCount != 0)// �޸ļ���ȫ����ͼʱBLOCK��֮���Y�����಻����� [10/31/2017 617004]
                p.Y = /*GlobalVar.firstBlockLocation + */GlobalVar.block_width * (a / (GlobalVar.firstBlockCount + 1)) /*+ 20 * (a / (GlobalVar.firstBlockCount + 1) + 1)*/;
            #endregion
            return p;
        }

        private void init_tabPage_mainview()
        {
            try
            {
                for (int m = 0; m < GlobalVar.gl_List_PointInfo.m_List_PointInfo.Count; m++)
                {
                    List<SPoint> onegroupPoint = GlobalVar.gl_List_PointInfo.m_List_PointInfo[m].m_ListGroup;
                    List<DetailBlock> blocklist = GlobalVar.gl_List_PointInfo.m_List_PointInfo[m].m_BlockList_ByGroup.m_BlockinfoList;

                    for (int i = 0; i < blocklist.Count; i++)
                    {
                        blocklist[i].Parent = tabPage_mainview;
                    }
                }
            }
            catch (Exception e)
            {
                throw new Exception("��ʼ��ȫ����ͼ����" + e.ToString());
            }
        }

        /* Closes the image provider when the window is closed. */
        private void MainForm_FormClosing(object sender, FormClosingEventArgs ev)
        {
            baslerCCD1.CCDClosing();
            m_form_movecontrol.Dispose();
            m_form_movecontrol.SetDO(m_form_movecontrol.m_AxisNum_U, 7, 0); //ǰ���ͷ�
            try
            {
                if (m_form_movecontrol.m_DeviceHandle != (IntPtr)(-1))
                {
                    m_form_movecontrol.CloseDevice();
                }
            }
            catch { }
            Application.Exit();
            //System.GC.Collect();

            //Thread.Sleep(50);
            //Application.ExitThread();
            //System.Environment.Exit(0);
        }

        #region �������ģ��
        //private void initImageProvider()
        //{
        //    try
        //    {
        //        /* Register for the events of the image provider needed for proper operation. */
        //        GlobalVar.gl_imageProvider.GrabErrorEvent += new ImageProvider.GrabErrorEventHandler(OnGrabErrorEventCallback);
        //        GlobalVar.gl_imageProvider.DeviceRemovedEvent += new ImageProvider.DeviceRemovedEventHandler(OnDeviceRemovedEventCallback);
        //        GlobalVar.gl_imageProvider.DeviceOpenedEvent += new ImageProvider.DeviceOpenedEventHandler(OnDeviceOpenedEventCallback);
        //        GlobalVar.gl_imageProvider.DeviceClosedEvent += new ImageProvider.DeviceClosedEventHandler(OnDeviceClosedEventCallback);
        //        GlobalVar.gl_imageProvider.GrabbingStartedEvent += new ImageProvider.GrabbingStartedEventHandler(OnGrabbingStartedEventCallback);
        //        GlobalVar.gl_imageProvider.ImageReadyEvent += new ImageProvider.ImageReadyEventHandler(OnImageReadyEventCallback);
        //        GlobalVar.gl_imageProvider.GrabbingStoppedEvent += new ImageProvider.GrabbingStoppedEventHandler(OnGrabbingStoppedEventCallback);

        //        /* Provide the controls in the lower left area with the image provider object. */
        //        sliderGain.MyImageProvider = GlobalVar.gl_imageProvider;
        //        sliderExposureTime.MyImageProvider = GlobalVar.gl_imageProvider;
        //        sliderHeight.MyImageProvider = GlobalVar.gl_imageProvider;
        //        sliderWidth.MyImageProvider = GlobalVar.gl_imageProvider;
        //        comboBoxTestImage.MyImageProvider = GlobalVar.gl_imageProvider;
        //        comboBoxPixelFormat.MyImageProvider = GlobalVar.gl_imageProvider;
        //        comboBoxTriggerActivation.MyImageProvider = GlobalVar.gl_imageProvider;
        //        comboBoxTriggerSource.MyImageProvider = GlobalVar.gl_imageProvider;
        //        comboBoxExposureAuto.MyImageProvider = GlobalVar.gl_imageProvider;
        //        comboBoxTriggerMode.MyImageProvider = GlobalVar.gl_imageProvider;

        //        /* Update the list of available devices in the upper left area. */
        //        UpdateDeviceList();

        //        /* Enable the tool strip buttons according to the state of the image provider. */
        //        EnableButtons(GlobalVar.gl_imageProvider.IsOpen, false);
        //    }
        //    catch (Exception e)
        //    { throw new Exception(e.ToString()); }
        //}

        ///* Handles the click on the single frame button. */
        //private void toolStripButtonOneShot_Click(object sender, EventArgs e)
        //{
        //    OneShot(); /* Starts the grabbing of one image. */
        //}

        ///* Handles the click on the continuous frame button. */
        //private void toolStripButtonContinuousShot_Click(object sender, EventArgs e)
        //{
        //    ContinuousShot(); /* Start the grabbing of images until grabbing is stopped. */
        //}

        ///* Handles the click on the stop frame acquisition button. */
        //private void toolStripButtonStop_Click(object sender, EventArgs e)
        //{
        //    Stop(); /* Stops the grabbing of images. */
        //}

        ///* Handles the event related to the occurrence of an error while grabbing proceeds. */
        //private void OnGrabErrorEventCallback(Exception grabException, string additionalErrorMessage)
        //{
        //    if (InvokeRequired)
        //    {
        //        /* If called from a different thread, we must use the BeginInvoke method to marshal the call to the proper thread. */
        //        BeginInvoke(new ImageProvider.GrabErrorEventHandler(OnGrabErrorEventCallback), grabException, additionalErrorMessage);
        //        return;
        //    }
        //    ShowException(grabException, additionalErrorMessage);
        //}

        ///* Handles the event related to the removal of a currently open device. */
        //private void OnDeviceRemovedEventCallback()
        //{
        //    if (InvokeRequired)
        //    {
        //        /* If called from a different thread, we must use the BeginInvoke method to marshal the call to the proper thread. */
        //        BeginInvoke(new ImageProvider.DeviceRemovedEventHandler(OnDeviceRemovedEventCallback));
        //        return;
        //    }
        //    /* Disable the buttons. */
        //    EnableButtons(false, false);
        //    /* Stops the grabbing of images. */
        //    Stop();
        //    /* Close the image provider. */
        //    CloseTheImageProvider();
        //    /* Since one device is gone, the list needs to be updated. */
        //    UpdateDeviceList();
        //}

        ///* Handles the event related to a device being open. */
        //private void OnDeviceOpenedEventCallback()
        //{
        //    if (InvokeRequired)
        //    {
        //        /* If called from a different thread, we must use the BeginInvoke method to marshal the call to the proper thread. */
        //        BeginInvoke(new ImageProvider.DeviceOpenedEventHandler(OnDeviceOpenedEventCallback));
        //        return;
        //    }
        //    /* The image provider is ready to grab. Enable the grab buttons. */
        //    EnableButtons(true, false);
        //}

        ///* Handles the event related to a device being closed. */
        //private void OnDeviceClosedEventCallback()
        //{
        //    if (InvokeRequired)
        //    {
        //        /* If called from a different thread, we must use the BeginInvoke method to marshal the call to the proper thread. */
        //        BeginInvoke(new ImageProvider.DeviceClosedEventHandler(OnDeviceClosedEventCallback));
        //        return;
        //    }
        //    /* The image provider is closed. Disable all buttons. */
        //    EnableButtons(false, false);
        //}

        ///* Handles the event related to the image provider executing grabbing. */
        //private void OnGrabbingStartedEventCallback()
        //{
        //    if (InvokeRequired)
        //    {
        //        /* If called from a different thread, we must use the BeginInvoke method to marshal the call to the proper thread. */
        //        BeginInvoke(new ImageProvider.GrabbingStartedEventHandler(OnGrabbingStartedEventCallback));
        //        return;
        //    }

        //    ///* Do not update device list while grabbing to avoid jitter because the GUI-Thread is blocked for a short time when enumerating. */
        //    //updateDeviceListTimer.Stop();
        //    //updateDeviceListTimer.Enabled = false;
        //    //updateDeviceListTimer = null;

        //    /* The image provider is grabbing. Disable the grab buttons. Enable the stop button. */
        //    EnableButtons(false, true);
        //}

        ///* ��ȡ��Ƭ  Handles the event related to an image having been taken and waiting for processing. */
        //private void OnImageReadyEventCallback()
        //{
        //    if (InvokeRequired)
        //    {
        //        /* If called from a different thread, we must use the BeginInvoke method to marshal the call to the proper thread. */
        //        BeginInvoke(new ImageProvider.ImageReadyEventHandler(OnImageReadyEventCallback));
        //        return;
        //    }
        //    try
        //    {
        //        /* Acquire the image from the image provider. Only show the latest image. The camera may acquire images faster than images can be displayed*/
        //        ImageProvider.Image image = GlobalVar.gl_imageProvider.GetLatestImage();

        //        /* Check if the image has been removed in the meantime. */
        //        if (image != null)
        //        {
        //            if (m_ScanAuthorized && m_tag_CalibrateOK)
        //            {
        //                m_wait_picReceived.Set();
        //            }
        //            /* Check if the image is compatible with the currently used bitmap. */
        //            if (BitmapFactory.IsCompatible(m_bitmap, image.Width, image.Height, image.Color))
        //            {
        //                /* Update the bitmap with the image data. */
        //                BitmapFactory.UpdateBitmap(m_bitmap, image.Buffer, image.Width, image.Height, image.Color);
        //                /* To show the new image, request the display control to update itself. */
        //                pictureBox_capture.Refresh();
        //            }
        //            else /* A new bitmap is required. */
        //            {
        //                BitmapFactory.CreateBitmap(out m_bitmap, image.Width, image.Height, image.Color);
        //                BitmapFactory.UpdateBitmap(m_bitmap, image.Buffer, image.Width, image.Height, image.Color);
        //                /* We have to dispose the bitmap after assigning the new one to the display control. */
        //                Bitmap bitmap = pictureBox_capture.Image as Bitmap;
        //                pictureBox_capture.Image = m_bitmap;
        //                if (bitmap != null)
        //                {
        //                    /* Dispose the bitmap. */
        //                    bitmap.Dispose();
        //                }
        //            }

        //            GlobalVar.gl_imageProvider.ReleaseImage();
        //            m_bitmap.RotateFlip(RotateFlipType.Rotate90FlipNone);
        //            if (m_bitmap_calibrate_REF == null) { m_bitmap_calibrate_REF = (Bitmap)m_bitmap.Clone(); return; }
        //            if (m_bitmap_calibrate_END == null) { m_bitmap_calibrate_END = (Bitmap)m_bitmap.Clone(); return; }
        //            if (m_ScanAuthorized && m_tag_CalibrateOK)
        //            {
        //                BitmapInfo bi = new BitmapInfo();
        //                bi.FlowID = GlobalVar.gl_CurrentFlowID;
        //                bi.bitmap = (Bitmap)m_bitmap.Clone();
        //                bi.num = m_current_num;
        //                m_list_bmpReceived.Add(bi);
        //            }
        //        }
        //    }
        //    catch (Exception e)
        //    {
        //        logWR.appendNewLogMessage("��Ƭ�ɼ�OnImageReadyEventCallback error : \r\n" + e.ToString());
        //    }
        //}

        //private Bitmap DrawDiagonalLines(Bitmap bmp)
        //{
        //    try
        //    {
        //        Bitmap result = new Bitmap(bmp.Width, bmp.Height, PixelFormat.Format32bppArgb);
        //        Graphics g = Graphics.FromImage(result);
        //        //g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        //        //g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
        //        //g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
        //        g.DrawImage(bmp, 0, 0);
        //        Pen p = new Pen(Color.Green, 4);
        //        p.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;
        //        g.DrawLine(p, new Point(0, bmp.Height / 2), new Point(bmp.Width, bmp.Height / 2));
        //        g.DrawLine(p, new Point(bmp.Width / 2, 0), new Point(bmp.Width / 2, bmp.Height));
        //        g.Save();
        //        return result;
        //    }
        //    catch {
        //        return new Bitmap(640,480);
        //    }
        //}

        ///* Handles the event related to the image provider having stopped grabbing. */
        //private void OnGrabbingStoppedEventCallback()
        //{
        //    if (InvokeRequired)
        //    {
        //        /* If called from a different thread, we must use the BeginInvoke method to marshal the call to the proper thread. */
        //        BeginInvoke(new ImageProvider.GrabbingStoppedEventHandler(OnGrabbingStoppedEventCallback));
        //        return;
        //    }

        //    /* Enable device list update again */
        //    //updateDeviceListTimer.Start();

        //    /* The image provider stopped grabbing. Enable the grab buttons. Disable the stop button. */
        //    EnableButtons(GlobalVar.gl_imageProvider.IsOpen, false);
        //}

        ///* Helps to set the states of all buttons. */
        //private void EnableButtons(bool canGrab, bool canStop)
        //{
        //    toolStripButtonContinuousShot.Enabled = canGrab;
        //    toolStripButtonOneShot.Enabled = canGrab;
        //    toolStripButtonStop.Enabled = canStop;
        //}

        ///* Stops the image provider and handles exceptions. */
        //private void Stop()
        //{
        //    /* Stop the grabbing. */
        //    try
        //    {
        //        GlobalVar.gl_imageProvider.Stop();
        //    }
        //    catch (Exception e)
        //    {
        //        ShowException(e, GlobalVar.gl_imageProvider.GetLastErrorMessage());
        //    }
        //}

        ///* Closes the image provider and handles exceptions. */
        //private void CloseTheImageProvider()
        //{
        //    /* Close the image provider. */
        //    try
        //    {
        //        GlobalVar.gl_imageProvider.Close();
        //    }
        //    catch (Exception e)
        //    {
        //        ShowException(e, GlobalVar.gl_imageProvider.GetLastErrorMessage());
        //    }
        //}

        ///* Starts the grabbing of one image and handles exceptions. */
        //private void OneShot()
        //{
        //    try
        //    {
        //        GlobalVar.gl_imageProvider.OneShot(); /* Starts the grabbing of one image. */
        //    }
        //    catch (Exception e)
        //    {
        //        ShowException(e, GlobalVar.gl_imageProvider.GetLastErrorMessage());
        //    }
        //}

        ///* Starts the grabbing of images until the grabbing is stopped and handles exceptions. */
        //private void ContinuousShot()
        //{
        //    try
        //    {
        //        GlobalVar.gl_imageProvider.ContinuousShot(); /* Start the grabbing of images until grabbing is stopped. */
        //    }
        //    catch (Exception e)
        //    {
        //        ShowException(e, GlobalVar.gl_imageProvider.GetLastErrorMessage());
        //    }
        //}

        ///* Updates the list of available devices in the upper left area. */
        //private void UpdateDeviceList()
        //{
        //    try
        //    {
        //        /* Ask the device enumerator for a list of devices. */
        //        List<DeviceEnumerator.Device> list = DeviceEnumerator.EnumerateDevices();

        //        ListView.ListViewItemCollection items = deviceListView.Items;

        //        /* Add each new device to the list. */
        //        foreach (DeviceEnumerator.Device device in list)
        //        {
        //            bool newitem = true;
        //            /* For each enumerated device check whether it is in the list view. */
        //            foreach (ListViewItem item in items)
        //            {
        //                /* Retrieve the device data from the list view item. */
        //                DeviceEnumerator.Device tag = item.Tag as DeviceEnumerator.Device;

        //                if ( tag.FullName == device.FullName)
        //                {
        //                    /* Update the device index. The index is used for opening the camera. It may change when enumerating devices. */
        //                    tag.Index = device.Index;
        //                    /* No new item needs to be added to the list view */
        //                    newitem = false;
        //                    break; 
        //                }
        //            }

        //            /* If the device is not in the list view yet the add it to the list view. */
        //            if (newitem)
        //            {
        //                ListViewItem item = new ListViewItem(device.Name);
        //                if (device.Tooltip.Length > 0)
        //                {
        //                    item.ToolTipText = device.Tooltip;
        //                }
        //                item.Tag = device;

        //                /* Attach the device data. */
        //                deviceListView.Items.Add(item);
        //                camConnectAutomatic();
        //            }
        //        }

        //        /* Delete old devices which are removed. */
        //        foreach (ListViewItem item in items)
        //        {
        //            bool exists = false;

        //            /* For each device in the list view check whether it has not been found by device enumeration. */
        //            foreach (DeviceEnumerator.Device device in list)
        //            {
        //                if (((DeviceEnumerator.Device)item.Tag).FullName == device.FullName)
        //                {
        //                    exists = true;
        //                    break; 
        //                }
        //            }
        //            /* If the device has not been found by enumeration then remove from the list view. */
        //            if (!exists)
        //            {
        //                deviceListView.Items.Remove(item);
        //            }
        //        }
        //    }
        //    catch (Exception e)
        //    {
        //        ShowException(e, GlobalVar.gl_imageProvider.GetLastErrorMessage());
        //    }
        //}

        ///* Shows exceptions in a message box. */
        //private void ShowException(Exception e, string additionalErrorMessage)
        //{
        //    string more = "\n\nLast error message (may not belong to the exception):\n" + additionalErrorMessage;
        //    MessageBox.Show("Exception caught:\n" + e.Message + (additionalErrorMessage.Length > 0 ? more : ""), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        //}

        ///* Handles the selection of cameras from the list box. The currently open device is closed and the first 
        // selected device is opened. */
        //private void deviceListView_SelectedIndexChanged(object sender, EventArgs ev)
        //{
        //    ConnectWebCam();
        //}

        //private void ConnectWebCam()
        //{
        //    /* Close the currently open image provider. */
        //    /* Stops the grabbing of images. */
        //    Stop();
        //    /* Close the image provider. */
        //    CloseTheImageProvider();

        //    /* Open the selected image provider. */
        //    //if (deviceListView.SelectedItems.Count > 0)
        //    if (deviceListView.Items.Count > 0)
        //    {
        //        /* Get the first selected item. */
        //        //ListViewItem item = deviceListView.SelectedItems[0];
        //        ListViewItem item = deviceListView.Items[0];
        //        /* Get the attached device data. */
        //        DeviceEnumerator.Device device = item.Tag as DeviceEnumerator.Device;
        //        try
        //        {
        //            /* Open the image provider using the index from the device data. */
        //            GlobalVar.gl_imageProvider.Open(device.Index);
        //        }
        //        catch (Exception e)
        //        {
        //            ShowException(e, GlobalVar.gl_imageProvider.GetLastErrorMessage());
        //        }
        //    }
        //}

        ///* If the F5 key has been pressed update the list of devices. */
        //private void deviceListView_KeyDown(object sender, KeyEventArgs ev)
        //{
        //    if (ev.KeyCode == Keys.F5)
        //    {
        //        ev.Handled = true;
        //        /* Update the list of available devices in the upper left area. */
        //        UpdateDeviceList();
        //    }
        //}

        ///* Timer callback used for periodically checking whether displayed devices are still attached to the PC. */
        //private void updateDeviceListTimer_Tick(object sender, EventArgs e)
        //{
        //    UpdateDeviceList(); //fotest
        //}
        #endregion

        /// <summary>
        /// ͼƬ�ɼ�������
        /// </summary>
        /// <param name="obj"></param>
        private void ProcessImage(object obj)
        {
            try
            {
                Thread thread = new Thread(new ThreadStart(
                    delegate
                    {
                        for (; ; )
                        {
                            try
                            {
                                if (GlobalVar.gl_inEmergence) { break; }
                                if (!GlobalVar.gl_safetyDoor_Front && GlobalVar.gl_usermode == 0) break;
                                bool finished = false;
                                //���FLOWID * һ��ѭ����MIC����
                                int TotalCircleCount = GlobalVar.gl_List_PointInfo.m_List_PointInfo[0].m_BlockList_ByGroup.m_BlockinfoList.Count
                                    * GlobalVar.gl_List_PointInfo.m_List_PointInfo.Count;
                                if (m_list_bmpReceived.Count == TotalCircleCount)  //ֱ���ɼ��������ж�
                                { finished = true; }
                                //m_list_bmpReceived�а���������Cycle�в�ͬFLOWID���̵���Ƭ������MIC1/MIC2����Ƭ��������
                                for (int m = 0; m < m_list_bmpReceived.Count; m++)
                                {
                                    finished &= m_list_bmpReceived[m].m_processed;
                                    if (m_list_bmpReceived[m].m_processed)
                                    { continue; }
                                    //m_list_bmpReceived[m].m_processed = true; �ƶ�������
                                    Bitmap bmp = m_list_bmpReceived[m].bitmap;
                                    for (int n = 0; n < GlobalVar.gl_List_PointInfo.m_List_PointInfo.Count; n++)
                                    {
                                        int flowid = GlobalVar.gl_List_PointInfo.m_List_PointInfo[n].FlowID;
                                        List<DetailBlock> blocklist = GlobalVar.gl_List_PointInfo.m_List_PointInfo[n].m_BlockList_ByGroup.m_BlockinfoList;

                                        for (int i = 0; i < blocklist.Count; i++)
                                        {
                                            if ((blocklist[i].m_PcsNo == m_list_bmpReceived[m].num)
                                                && (blocklist[i].flowid == m_list_bmpReceived[m].FlowID))  //��Ϊ�в�ͬ��FLOWID������PCSNOһ��
                                            {
                                                blocklist[i].m_sheetbarcode = m_barcodeinfo_CurrentUse.barcode;
                                                blocklist[i]._bitmap = (Bitmap)(bmp.Clone());
                                                m_list_bmpReceived[m].m_processed = true; //�ƶ�������
                                            }
                                        }
                                    }
                                }
                                if (finished) break;
                            }
                            catch { }
                            Thread.Sleep(200);
                        }
                    }));
                thread.IsBackground = true;
                thread.Start();
            }
            catch (Exception e)
            { }
        }

        //���òο�ԭ������
        void m_obj_dwg_eve_sendReFPoint(SPoint spoint)
        {
            if (m_form_movecontrol.CheckAxisInMoving())
            {
                MessageBox.Show("�豸(��)ΪNot Ready״̬����ֹͣ���������I��", "��ʾ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try
            {
                Thread t = new Thread(new ThreadStart(delegate
                    {
                        GlobalVar.gl_Ref_Point_Axis.Pos_X = spoint.Pos_X;
                        GlobalVar.gl_Ref_Point_Axis.Pos_Y = spoint.Pos_Y;
                        myfunc.WriteRefPositionInfoToTBS();
                        if (m_form_movecontrol.AllAxisBackToMachanicalOrgPoint())
                        {
                            m_form_movecontrol.WaitAllMoveFinished();
                            m_form_movecontrol.MovetoRefPoint();
                        }
                        else
                        {
                            ShowPsdErrForm err = new ShowPsdErrForm("�豸��ԭ�����ʧ�ܣ�������������λ��!", false);
                            err.ShowDialog();
                            m_form_movecontrol.CloseDevice();
                            Application.Exit();
                        }
                    }));
                t.IsBackground = true;
                t.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        /// <summary>
        /// �����˶��K��λ�C��������
        /// </summary>
        /// <param name="Pos_X"></param>
        /// <param name="Pos_Y"></param>
        /// <param name="multiple"></param>
        /// <param name="OnlyCapture">����Ҫ�˶���ֱ������,x,y����Ϊ0����</param>
        void FixPointMotionAndCapture(float Pos_X, float Pos_Y, int multiple, bool OnlyCapture)
        {
            try
            {
                if (!OnlyCapture)
                {
                    //�ȴ��˶����
                    m_form_movecontrol.WaitAllMoveFinished();
                    Thread.Sleep(20);
                    Pos_X = Pos_X * -1; //��еԭ��������,X������Ҫȡ��
                    m_form_movecontrol.FixPointMotion(Pos_X, Pos_Y, multiple);
                    m_form_movecontrol.WaitAllMoveFinished();
                    Thread.Sleep(120);
                }
                baslerCCD1.StartOneShot(); //ModbusͨѶ���˶��������
                //m_form_movecontrol.CaptureTrigger(); //����
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        #region MODBUSͨӍ
        //���͵�λ��У������ֵ(������Z������);
        void SendReviseValue(SPoint spoint)
        {
            try
            {
                InputModule module = new InputModule();
                module.byFuntion = 16;
                module.bySlaveID = 1;
                module.nDataLength = 2;
                module.nStartAddr = 4;

                byte[] data = new Byte[4];
                short pos_x = short.Parse((spoint.Pos_X * GlobalVar.gl_PixelDistance).ToString("00000"));
                byte[] array_x = BitConverter.GetBytes((short)IPAddress.HostToNetworkOrder(pos_x));
                data[0] = array_x[0];
                data[1] = array_x[1];
                short pos_y = short.Parse((spoint.Pos_Y * GlobalVar.gl_PixelDistance).ToString("00000"));
                byte[] array_y = BitConverter.GetBytes((short)IPAddress.HostToNetworkOrder(pos_y));
                data[2] = array_y[0];
                data[3] = array_y[1];
                module.byWriteData = data;
                m_modbus.SendMessage(module);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        //�ؙCеԭ�c
        void returnMachicalOrgPoint()
        {
            if (m_inScanFunction) { return; }
            try
            {
                InputModule module1 = new InputModule();
                module1.byFuntion = 5;
                module1.bySlaveID = 1;
                module1.nDataLength = 1;
                module1.nStartAddr = 8;

                byte[] data = new Byte[2];
                data[0] = 0xff;
                data[1] = 0x00;
                module1.byWriteData = data;
                m_modbus.SendMessage(module1);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        //�؅���ԭ�c
        void returnreferecePoint()
        {
            if (m_inScanFunction) { return; }
            if (m_form_movecontrol.CheckAxisInMoving())
            {
                MessageBox.Show("�豸(��)ΪNot Ready״̬����ֹͣ���������I��", "��ʾ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try
            {
                //�ȴ��˶����
                m_form_movecontrol.WaitAllMoveFinished();
                Thread.Sleep(200);
                m_form_movecontrol.FixPointMotion(0, 0, 2);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        //���칤��ģʽ 0����������ģʽ 0x00   1: ����ͨ�з�ʽ 0xff
        void SetRailWorkMode(int mode)
        {
            if (m_inScanFunction) { return; }
            try
            {
                InputModule module1 = new InputModule();
                module1.byFuntion = 5;
                module1.bySlaveID = 1;
                module1.nDataLength = 1;
                module1.nStartAddr = 0;

                byte[] data = new Byte[2];
                if (mode == 0)
                {
                    data[0] = 0x00;
                }
                else
                {
                    data[0] = 0xff;
                }
                data[1] = 0x00;
                module1.byWriteData = data;
                m_modbus.SendMessage(module1);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        //�����l�a����Y���_�J---ɨ�赽�����֪ͨ��λ��
        void SheetBarcodeScanPass()
        {
            if (m_inScanFunction) { return; }
            try
            {
                InputModule module1 = new InputModule();
                module1.byFuntion = 5;
                module1.bySlaveID = 1;
                module1.nDataLength = 1;
                module1.nStartAddr = 6;

                byte[] data = new Byte[2];
                data[0] = 0xff;
                data[1] = 0x00;
                module1.byWriteData = data;
                m_modbus.SendMessage(module1);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        //���ع�Դ
        void LEDOnOff(bool cmd)
        {
            if (m_inScanFunction) { return; }
            try
            {
                InputModule module1 = new InputModule();
                module1.byFuntion = 5;
                module1.bySlaveID = 1;
                module1.nDataLength = 1;
                module1.nStartAddr = 3;

                byte[] data = new Byte[2];
                if (cmd) { data[0] = 0xff; }
                else { data[0] = 0x00; }
                data[1] = 0x00;
                module1.byWriteData = data;
                m_modbus.SendMessage(module1);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        Color forecolor = Color.Red;
        private void AppendRichText(string str)
        {
            if (forecolor == Color.Red) { forecolor = Color.Green; }
            else { forecolor = Color.Red; }
            this.BeginInvoke(new Action(() =>
                {
                    richTextBox_record.AppendText(str);
                    richTextBox_record.ScrollToCaret();

                    try
                    {
                        int p_start = richTextBox_record.TextLength - str.Length;
                        p_start = p_start < 0 ? 0 : p_start;
                        richTextBox_record.Select(p_start, str.Length);
                        richTextBox_record.SelectionColor = forecolor;
                    }
                    catch { }
                }
                ));
        }

        //����ֹͣ�������������
        private void EmergenceReset()
        {
            m_barcodeinfo_CurrentUse.Clear();
            m_barcodeinfo_preScan.Clear();
            m_current_num = 0;
            OneCircleReset();
            clearTags();

            m_coilstatus_CCDTrigger = m_coilstatus_EmergenceError = m_coilstatus_FixScan = m_coilstatus_Led
                = m_coilstatus_ShiftToStage2 = m_coilstatus_Stage2Arrived
                 = false;
        }

        private DataTable GetNGPositions(string Barcode)
        {
            DataTable dt = new DataTable();
            try
            {
                string sql = "SELECT TOP 100 [SHEETSN] ,[PCSNO] FROM [BARDATA].[dbo].[AutoPunchDataDetail] where SHEETSN = '" + Barcode + "'" + " and PARTNO = 'BM'";
                dt = m_DBQuery.get_database_BARDATA(sql);
            }
            catch { }
            return dt;
        }

        private bool m_manualCycleReset = false;
        private void cycleCheckAllDecodeFinished()
        {
            try
            {
                Thread threadCheck = new Thread(new ThreadStart(
                    delegate
                    {
                        Thread.Sleep(20);
                        bool finished = false;
                        bool TypeCheck = true;  //��Ʒ���ϼ��
                        try
                        {
                            m_manualCycleReset = false;
                            int totalValidPcs = 0;
                            int totalFailed = 0;
                            while (!finished)
                            {
                                if (GlobalVar.gl_inEmergence) { return; }
                                if (!GlobalVar.gl_safetyDoor_Front && GlobalVar.gl_usermode == 0) return;
                                finished = true;
                                TypeCheck = true;
                                totalValidPcs = 0;
                                totalFailed = 0;
                                Thread.Sleep(100);
                                for (int n = 0; n < GlobalVar.gl_List_PointInfo.m_List_PointInfo.Count; n++)
                                {
                                    //����block����
                                    OneGroup_Blocks BlocksGroup = GlobalVar.gl_List_PointInfo.m_List_PointInfo[n].m_BlockList_ByGroup;
                                    List<DetailBlock> blockList = BlocksGroup.m_BlockinfoList;
                                    for (int i = 0; i < blockList.Count; i++)
                                    {
                                        if (GlobalVar.gl_inEmergence) { return; }
                                        if (!GlobalVar.gl_safetyDoor_Front && GlobalVar.gl_usermode == 0) return;
                                        if (blockList[i].m_receivedPics)
                                        {
                                            if (blockList[i].m_receivedPics
                                                && blockList[i].m_GoodPostion)
                                            {
                                                totalValidPcs++;
                                            }
                                            if (GlobalVar.gl_LinkType != LinkType.BARCODE)
                                            {
                                                TypeCheck &= blockList[i].m_TypeCheck;
                                            }
                                            finished &= blockList[i].m_decodeFinished;
                                            if (!blockList[i].m_result
                                                && blockList[i].m_GoodPostion)
                                            { totalFailed++; }
                                        }
                                    }
                                }
                                if (m_manualCycleReset)
                                { finished = true; }
                            }
                            GlobalVar.gl_testinfo_totalSheet++; //ltt
                            GlobalVar.gl_testinfo_totalTest += totalValidPcs;
                            GlobalVar.gl_testinfo_decodefailed += totalFailed;
                            //����д�����Ҫ��������Ȼ�������ذ��˳�
                            if (!TypeCheck)
                            {
                                logWR.appendNewLogMessage("�����e�`,��ǰƷĿ�������c���H���ϲ�һ�£�Ո�z��!");
                                updateLedLightStatus(2);
                                ShowPsdErrForm sp = new ShowPsdErrForm("�����e�`,��ǰƷĿ�������c���H���ϲ�һ�£�Ո�z��!", true);
                                //ShowPsdErrForm sp = new ShowPsdErrForm("�����e�`,��ǰƷĿ�����Ϟ�:" + GlobalVar.gl_list_ZhiPinInfo[0]._SubName
                                //    + ", Barcode�^����:" + GlobalVar.gl_list_ZhiPinInfo[0]._HeadStr + "��Ո�z��!", true);
                                sp.ShowDialog();
                                updateLedLightStatus(0);
                            }
                            else
                            {
                                int ng_Num = 0;
                                for (int n = 0; n < GlobalVar.gl_List_PointInfo.m_List_PointInfo.Count; n++)
                                {
                                    //����block���� 
                                    OneGroup_Blocks BlocksGroup = GlobalVar.gl_List_PointInfo.m_List_PointInfo[n].m_BlockList_ByGroup;
                                    int flowid = GlobalVar.gl_List_PointInfo.m_List_PointInfo[n].FlowID;
                                    List<DetailBlock> blockList = BlocksGroup.m_BlockinfoList;
                                    logWR.appendNewLogMessage("���н�����ϣ���������");
                                    //���н�����ϣ���������
                                    string str_content = "[" + GlobalVar.gl_iniSection_Result + "]\r\n";
                                    //���ݿ�������Ϣ
                                    str_content += GlobalVar.gl_iniKey_ConnStr;
                                    str_content += "=";
                                    str_content += GlobalVar.gl_DataBaseConnectString;
                                    str_content += "\r\n";
                                    str_content += GlobalVar.gl_iniKey_FlowID;
                                    str_content += "=";
                                    str_content += flowid;
                                    str_content += "\r\n";
                                    //������Ϣ
                                    for (int i = 0; i < blockList.Count; i++)
                                    {
                                        str_content += blockList[i].m_PcsNo_Mapping.ToString();
                                        str_content += "=";
                                        str_content += blockList[i].m_resultString;
                                        if ((!blockList[i].m_result) && blockList[i].m_GoodPostion) ng_Num++;
                                        str_content += "\r\n";
                                    }
                                    //����洢Ϊsheetno_flowid.ini
                                    myfunc.SaveResultINIString(m_barcodeinfo_CurrentUse.barcode + "_" + flowid.ToString(), str_content);

                                }
                                logWR.appendNewLogMessage("�����������,sheetbarcode:" + m_barcodeinfo_CurrentUse.barcode);
                                if (ng_Num > 3)
                                {
                                    updateLedLightStatus(2);
                                    ShowPsdErrForm form = new ShowPsdErrForm("����ʧ�ܸ������࣬�����պϹ�����", true);
                                    form.ShowDialog();
                                    updateLedLightStatus(0);
                                    ng_Num = 0;
                                }
                                //GlobalVar.gl_testinfo_totalSheet++; 
                                //GlobalVar.gl_testinfo_totalTest += totalValidPcs;
                                //GlobalVar.gl_testinfo_decodefailed += totalFailed;
                                m_manualCycleReset = false;
                                //BeginInvoke(new Action(() =>
                                //{
                                //    updatetestinfo();
                                //}));
                            }
                            clearTags();
                        }
                        catch (Exception ex)
                        {
                            updateLedLightStatus(2);
                            logWR.appendNewLogMessage("�����쳣:" + ex.ToString());
                            ShowPsdErrForm form = new ShowPsdErrForm("�����쳣,�����¹���", true);
                            form.ShowDialog();
                            updateLedLightStatus(0);
                        }

                        m_form_movecontrol.Stage2ZaibanPass(); //֪ͨ�����ذ��˳�
                        //richTextBox_SingleShow.AppendText(DateTime.Now.ToString("yyyyMMdd HH:mm:ss") + "\t�����ذ��˳�");
                        AddShowLog(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + "\r\n�����ذ��˳�", Color.Green);
                        BeginInvoke(new Action(() =>
                        {
                            updatetestinfo();
                        }));
                        m_count_BoardIn--;
                        updateLedLightStatus(0);
                        m_tag_InCheckAllDecodeFinished = false;
                        m_tag_DBQueryFinished = false;
                        m_coilstatus_ShiftToStage2 = false;
                        //GC.Collect();
                    }))
                {
                    IsBackground = true
                };
                threadCheck.Start();
            }
            catch
            { }
        }

        //���������Ϣ
        private void SaveTestInfo()
        {
            string iniFilePath = GlobalVar.gl_strTargetPath + "\\" + GlobalVar.gl_iniTBS_FileName;
            MyFunctions.WritePrivateProfileString(GlobalVar.gl_inisection_TestInfo, GlobalVar.gl_iniKey_TotalTest, GlobalVar.gl_testinfo_totalTest.ToString(), iniFilePath);
            MyFunctions.WritePrivateProfileString(GlobalVar.gl_inisection_TestInfo, GlobalVar.gl_iniKey_TotalDecodeFailed, GlobalVar.gl_testinfo_decodefailed.ToString(), iniFilePath);
            MyFunctions.WritePrivateProfileString(GlobalVar.gl_inisection_TestInfo, GlobalVar.gl_iniKey_TotalSheets, GlobalVar.gl_testinfo_totalSheet.ToString(), iniFilePath);
        }
        #endregion

        #region �ߴa���ڲ�������
        private void openScanPort()
        {
            try
            {
                if (serialPort_scan.IsOpen)
                {
                    serialPort_scan.Close();
                }
                string[] ports = SerialPort.GetPortNames();
                if (ports.Length == 1)
                    serialPort_scan.PortName = ports[0];
                else
                    serialPort_scan.PortName = GlobalVar.gl_serialPort_Scan;
                serialPort_scan.Open();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("���ڴ��_ʧ����" + ex.Message);
            }
        }

        private void StartScanPanelBarcodefortest(object obj)
        {
            byte[] byteArry_startScan = new byte[3];
            byteArry_startScan[0] = 0x16;
            byteArry_startScan[1] = 0x54;
            byteArry_startScan[2] = 0x0D;
            try
            {

                for (int i = 0; i < 100; i++)   //read time 3s
                {
                    serialPort_scan.DiscardInBuffer();
                    serialPort_scan.ReadTimeout = 500;
                    byteArry_startScan[1] = 0x54;
                    serialPort_scan.Write(byteArry_startScan, 0, 3);
                    try
                    {
                        byte[] byteArray = new byte[serialPort_scan.BytesToRead];
                        string totalstr = serialPort_scan.ReadTo("\r\n");
                        if ((totalstr.Length > 4) && (myfunc.checkStringIsLegal(totalstr, 3)))
                        {
                            m_barcodeinfo_preScan.barcode = totalstr;
                            break;
                        }
                    }
                    catch { }
                    byteArry_startScan[1] = 0x55;
                    serialPort_scan.Write(byteArry_startScan, 0, 3);
                }
                if (m_barcodeinfo_preScan.barcode.Trim().Length > 0)
                {
                }
            }
            catch (System.Exception ex)
            { }
        }


        private void StartScanPanelBarcode(object obj)
        {
            byte[] byteArry_startScan = new byte[3];
            byteArry_startScan[0] = 0x16;
            byteArry_startScan[1] = 0x54;
            byteArry_startScan[2] = 0x0D;
            //byteArry_startScan[3] = 0x0A;
            //ThreadPool.QueueUserWorkItem(AsyReceiveData);
            try
            {

                for (int i = 0; i < 3; i++)   //read time 3s
                {
                    serialPort_scan.DiscardInBuffer();
                    serialPort_scan.ReadTimeout = 500;
                    byteArry_startScan[1] = 0x54;
                    serialPort_scan.Write(byteArry_startScan, 0, 3);
                    try
                    {
                        Thread.Sleep(100);
                        byte[] byteArray = new byte[serialPort_scan.BytesToRead];
                        string totalstr = serialPort_scan.ReadTo("\r\n");
                        //string totalstr = serialPort_scan.ReadExisting().Trim('\r').Trim('\n').Trim();
                        //if ((totalstr.Length > 4) && (myfunc.checkStringIsLegal(totalstr, 3))) //ltt
                        if (totalstr.Length > 4)
                        {
                            m_barcodeinfo_preScan.barcode = totalstr;
                            break;
                        }
                    }
                    catch { }
                    byteArry_startScan[1] = 0x55;
                    serialPort_scan.Write(byteArry_startScan, 0, 3);
                }
                if (m_barcodeinfo_preScan.barcode.Trim().Length > 0)
                {
                    QueryNGPositionsFromDB(m_barcodeinfo_preScan.barcode); //һ��Ҫ��ѯ��ϲ����˶�����������ݲ��ԡ�
                    QuerySheetNoLotInfo(m_barcodeinfo_preScan.barcode);
                    AutoGeneralSheetFolder(m_barcodeinfo_preScan.barcode.Trim());
                }
            }
            catch (System.Exception ex)
            {
                logWR.appendNewLogMessage("ɨ��SHEET��������쳣��   StartScanPanelBarcode   \r\n" + ex.ToString());
            }
            finally
            {
                DateTime dt = DateTime.Now;
                //���ذ��˶��쳣�����ж�  --20171011 lqz
                while (GlobalVar.gl_bUseLargeBoardSize && m_count_BoardIn != 1)
                {
                    Thread.Sleep(50);//���ʹ�ô�岢�һ������ذ�����Ψһ���������ź�
                    DateTime rt = DateTime.Now;
                    TimeSpan sp = rt - dt;
                    if (sp.Seconds > 20) break;
                }

                m_form_movecontrol.Stage1ZaibanPass();
                //richTextBox_SingleShow.AppendText(DateTime.Now.ToString("yyyyMMdd HH:mm:ss") + "\tһ���ذ��˳�");
                AddShowLog(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + "\r\nһ���ذ��˳�", Color.Green);
            }
        }

        #region OLD��ʽ����������
        //private void StartScanPanelBarcode(object obj)
        //{
        //    try
        //    {
        //        byte[] byteArry_startScan = new byte[3];
        //        byteArry_startScan[0] = 0x16;
        //        byteArry_startScan[1] = 0x54;
        //        byteArry_startScan[2] = 0x0D;
        //        //byteArry_startScan[3] = 0x0A;
        //        //ThreadPool.QueueUserWorkItem(AsyReceiveData);

        //        for (int i = 0; i < 1; i++)   //read time 3s
        //        {
        //            byteArry_startScan[1] = 0x54;
        //            serialPort_scan.Write(byteArry_startScan, 0, 3);
        //            Thread.Sleep(1000);
        //            try
        //            {
        //                byte[] byteArray = new byte[serialPort_scan.BytesToRead];
        //                serialPort_scan.Read(byteArray, 0, serialPort_scan.BytesToRead);
        //                string totalstr = System.Text.Encoding.Default.GetString(byteArray);
        //                if (totalstr.IndexOf("\r\n") > 0)
        //                {
        //                    totalstr = totalstr.Substring(0, totalstr.LastIndexOf("\r\n"));  //ɾ����Ч��β
        //                    if (totalstr.IndexOf("\r\n") > 0)
        //                    {
        //                        totalstr = totalstr.Substring(totalstr.LastIndexOf("\r\n") + 2);  //ɾ����Ч��β
        //                    }
        //                }
        //                //if ((totalstr.Length == GlobalVar.gl_length_sheetBarcodeLength) && (myfunc.checkStringIsLegal(totalstr, 3)))
        //                if (myfunc.checkStringIsLegal(totalstr, 3))
        //                {
        //                    m_barcodeinfo_preScan.barcode = totalstr;
        //                    break;
        //                }
        //            }
        //            catch { }
        //            byteArry_startScan[1] = 0x55;
        //            serialPort_scan.Write(byteArry_startScan, 0, 3);
        //        }
        //        if (m_barcodeinfo_preScan.barcode.Trim().Length > 0)
        //        {
        //            QueryNGPositionsFromDB(m_barcodeinfo_preScan.barcode); //һ��Ҫ��ѯ��ϲ����˶�����������ݲ��ԡ�
        //            QuerySheetNoLotInfo(m_barcodeinfo_preScan.barcode);
        //            AutoGeneralSheetFolder(m_barcodeinfo_preScan.barcode.Trim());
        //        }
        //    }
        //    catch (System.Exception ex)
        //    {
        //        logWR.appendNewLogMessage("ɨ��SHEET��������쳣��   StartScanPanelBarcode   \r\n" + ex.ToString());
        //    }
        //    finally
        //    {
        //        m_form_movecontrol.Stage1ZaibanPass();
        //    }
        //}
        #endregion
        private delegate void InvokeCallback(string msg, Color color);
        //���ӽ�����ʾ����
        private void AddShowLog(string msg, Color color)
        {
            this.Invoke(new Action(() =>
            {
                richTextBox_SingleShow.AppendText("\n");
                richTextBox_SingleShow.SelectionColor = color;
                richTextBox_SingleShow.AppendText(msg);
            }));
        }

        private void AutoGeneralSheetFolder(string sheetbarcode)
        {
            string saveDic = GlobalVar.gl_PicsSavePath + "\\" + sheetbarcode;
            if (!Directory.Exists(saveDic))
            {
                Directory.CreateDirectory(saveDic);
            }
            else
            {
                System.IO.Directory.Move(saveDic, saveDic + "_" + DateTime.Now.ToString("MMddHHmmssffff"));
            }
        }

        private void QueryNGPositionsFromDB(string barcode)
        {
            try
            {
                DataTable m_datatable_NGPositions = new DataTable();
                if (GlobalVar.gl_LinkType != LinkType.BARCODE)
                    m_datatable_NGPositions = GetNGPositions(m_barcodeinfo_preScan.barcode);
                //m_datatable_NGPositions = GetNGPositions("G2238706ZY");

                if (m_datatable_NGPositions.Rows.Count > 0)
                {
                    for (int i = 0; i < m_datatable_NGPositions.Rows.Count; i++)
                    {
                        try
                        {
                            m_barcodeinfo_preScan.NGPositionlist.Add(Convert.ToInt32(m_datatable_NGPositions.Rows[i]["PCSNO"].ToString()));
                        }
                        catch { }
                    }
                }
            }
            catch (Exception e)
            {
                logWR.appendNewLogMessage("���ݿ��ѯSheet����NGλ���쳣 QueryNGPositionsFromDB Err:  \r\n" + e.ToString());
            }
        }

        private void QuerySheetNoLotInfo(string barcode) //�ж�������LOT���Ƿ�ƥ�䣬�����ƥ�䣬�˳�
        {
            string sql = "SELECT TOP 1 LOTNO FROM [BARDATA].[dbo].[AutoPunchData] where SHEETSN = '" + barcode + "'";
            DataTable dt = m_DBQuery.get_database_BARDATA(sql);
            string lotno_db = "";
            try
            {
                if ((dt != null) && (dt.Rows.Count > 0))
                {
                    lotno_db = dt.Rows[0]["LOTNO"].ToString();
                    if (lotno_db != GlobalVar.gl_str_LotNo)
                    {
                        m_barcodeinfo_preScan.LotResult = false;
                        m_barcodeinfo_preScan.ErrMsg_Lot = "SHEET�l�aLOTNO����";
                    }
                }
            }
            catch { }
        }

        ////�ж�������LOT���Ƿ�ƥ�䣬�����ƥ�䣬�˳�
        //private void GetLotNoBySheetSN(string SheetSN)
        //{
        //    string lotno = "";
        //    DataTable dt = new DataTable();
        //    try
        //    {
        //        string sql = "SELECT TOP 1 LotNo FROM [BARDATA].[dbo].[AutoPunchData] where SHEETSN = '" + SheetSN + "'";
        //        dt = m_DBQuery.get_database_cmd(sql);

        //        if (dt.Rows.Count > 0)
        //        {
        //            lotno = dt.Rows[0]["LotNo"].ToString();
        //        }
        //    }
        //    catch { }
        //    m_barcodeinfo_preScan.LotNo = lotno;
        //}


        public void manualBarCodeScan()
        {
            if (!serialPort_scan.IsOpen)
            {
                MessageBox.Show("ɨ�洮��δ��");
                return;
            }
            byte[] byteArry_startScan = new byte[3];
            byteArry_startScan[0] = 0x16;
            byteArry_startScan[1] = 0x54;
            byteArry_startScan[2] = 0x0D;
            serialPort_scan.Write(byteArry_startScan, 0, 3);
        }

        //����ɨ����շ�ʽ����������
        private void AsyReceiveData2(object serialPortobj)
        {
            Thread.Sleep(100);
            for (int m = 0; m < 3; m++)
            {
                try
                {
                    string str = serialPort_scan.ReadTo("\r\n");
                    if (!myfunc.checkStringIsLegal(str, 3)) { continue; }
                    if (str.Length > 5)
                    {
                        try
                        {
                            this.BeginInvoke(new Action(() =>
                            {
                                try
                                {
                                    m_barcodeinfo_preScan.barcode = str;
                                    button_sheetSNInfo.Text = str;
                                    button_sheetSNInfo.BackColor = Color.Green;
                                }
                                catch { }
                            }
                            ));
                            //if (!m_coilstatus_workmode) { return; }
                            if (m_barcodeinfo_preScan.barcode.Trim().Length > 0)
                            {
                                SheetBarcodeScanPass();
                                //m_datatable_NGPositions = GetNGPositions(m_barcode_preScan);
                                //m_datatable_NGPositions = GetNGPositions("G2238706ZY");

                                ////---�޸ĳɶ��������ѯ
                                //if (m_datatable_NGPositions != null)
                                //{
                                //    if (m_datatable_NGPositions.Rows.Count > 0)
                                //    {
                                //        for (int i = 0; i < m_datatable_NGPositions.Rows.Count; i++)
                                //        {
                                //            try
                                //            {
                                //                m_listNGPosition_preScan.Add(Convert.ToInt32(m_datatable_NGPositions.Rows[i]["PCSNO"].ToString()));
                                //            }
                                //            catch { }
                                //        }
                                //    }
                                //}
                            }
                        }
                        catch { }
                        m_tag_DBQueryFinished = true;
                        break;
                    }
                }
                catch { }
            }
        }
        #endregion

        private void toolStripButtonSave_Click(object sender, EventArgs e)
        {
            if (m_bitmap != null)
            {
                m_bitmap.Save("d:\\capture.bmp");
            }
        }

        private void toolStripButton_LoadCADFile_Click(object sender, EventArgs e)
        {
            m_obj_dwg.OpenFile();
        }

        private void toolStripButton_Exit_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("�_���P�]ܛ����", "��ʾ", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1)
                == System.Windows.Forms.DialogResult.Yes)
            {
                // ɾ��ԭ�еĳ��� [11/10/2017 617004]
                //                 if (Application.StartupPath!=GlobalVar.gl_strAppPath)
                //                 {
                //                     Directory.Delete(Application.StartupPath);
                //                 }
                Application.Exit();
            }
        }

        private void toolStripButton_sendTPsMessage_Click(object sender, EventArgs e)
        {
            ThreadPool.QueueUserWorkItem(StartScanPanelBarcodefortest);
        }

        private void button_workpermitted_Click(object sender, EventArgs e)
        {
            m_form_movecontrol.SetDO(m_form_movecontrol.m_AxisNum_U, 7, 1); //ǰ������
            Thread.Sleep(100);
            if (!GlobalVar.gl_safetyDoor_Front && GlobalVar.gl_usermode == 0)
            {
                MessageBox.Show("��ȫ��δ����Ո��飡");
                return;
            }
            if (!Directory.Exists(GlobalVar.gl_PicsSavePath))
            {
                try
                {
                    Directory.CreateDirectory(GlobalVar.gl_PicsSavePath);
                }
                catch
                {
                    MessageBox.Show("�DƬ����·��" + GlobalVar.gl_PicsSavePath + "�����ڣ�Ո�����ƶ��DƬ����·����");
                }
                //MessageBox.Show("�DƬ����·��" + GlobalVar.gl_PicsSavePath + "�����ڣ�Ո�����ƶ��DƬ����·����");
                return;
            }
            if (GlobalVar.gl_str_Product.Trim() == "")
            {
                MessageBox.Show("LOTNO��գ�Ոݔ��LOTNO���@ȡƷĿ��Ϣ��");
                return;
            }
            if (GlobalVar.gl_List_PointInfo.m_List_PointInfo[0].m_ListGroup.Count <= 0)
            {
                MessageBox.Show("δ����ƷĿCAD�ęn��Ո�_�J��");
                return;
            }
            //if (GlobalVar.gl_str_MICHeadStr.Trim() == "")
            //{
            //    MessageBox.Show("�]��ԃ����ǰLOT������MICоƬ��Ϣ��Ո�_�J��");
            //    return;
            //}
            if (m_DBQuery == null)
            {
                MessageBox.Show("Ո���M��ƷĿ����!");
                return;
            }
            WorkPermitted(true);
            baslerCCD1.StopCCD();
            startUpload();//��ʼ�ϴ��ļ�
        }

        private void WorkPermitted(bool enable)
        {
            GlobalVar.m_ScanAuthorized = enable;
            button_workProhabit.Enabled = enable;
            button_workpermitted.Enabled = !enable;
            if (!enable)
            {
                button_status.BackColor = Color.Gray;
                button_status.Text = "���C��";
            }
            else
            {
                button_status.BackColor = Color.BlueViolet;
                button_status.Text = "���I��";
            }
        }
        private void button_workProhabit_Click(object sender, EventArgs e)
        {
            m_form_movecontrol.SetDO(m_form_movecontrol.m_AxisNum_U, 7, 0); //ǰ���ͷ�
            WorkPermitted(false);
        }


        DateTime timetest;
        private void startScanFunction()
        {
            try
            {
                timetest = DateTime.Now;
                OneCircleReset();
                checkBlocksIsValid();  //--�ƶ����������ڽ��в�ѯ
                CalibrateAction();
            }
            catch (Exception e)
            {
                logWR.appendNewLogMessage("����ɨ������쳣 startScanFunction Error:  \r\n " + e.ToString());
            }
        }

        //������Block�Ƿ���Ҫ���н����������׻�λ���ж�Ӧλ�ã��򲻽���
        private void checkBlocksIsValid()
        {
            for (int n = 0; n < GlobalVar.gl_List_PointInfo.m_List_PointInfo.Count; n++)
            {
                OneGroup_Blocks onegroupBlock = GlobalVar.gl_List_PointInfo.m_List_PointInfo[n].m_BlockList_ByGroup;
                List<DetailBlock> blocklist = onegroupBlock.m_BlockinfoList;
                try
                {
                    for (int i = 0; i < blocklist.Count; i++)
                    {
                        if (m_barcodeinfo_CurrentUse.NGPositionlist.Contains(blocklist[i].m_PcsNo_Mapping))
                        {
                            blocklist[i].m_GoodPostion = false;
                        }
                    }
                }
                catch { }
            }
            m_barcodeinfo_CurrentUse.NGPositionlist.Clear();
        }

        private void runCommand(object obj)
        {
            try
            {
                Thread.Sleep(80);
                m_form_movecontrol.WaitAllMoveFinished();
                Thread.Sleep(80);
                m_form_movecontrol_eve_MotionMsg("�������չ�����ҵ��");
                ThreadPool.QueueUserWorkItem(ProcessImage);
                //float pos_x,pos_y;

                //Thread thread = new Thread(CycleDecodeAllBlocks);
                //thread.IsBackground = true;
                //thread.Start();
                for (int n = 0; n < GlobalVar.gl_List_PointInfo.m_List_PointInfo.Count; n++)
                {
                    if (!GlobalVar.gl_safetyDoor_Front && GlobalVar.gl_usermode == 0) break;
                    //ÿ�鿪ʼ���m_current_num
                    m_current_num = 0;
                    //ÿ��ɨ�趼��Ҫ��ԭ��
                    m_form_movecontrol.FixPointMotion(0, 0, 3);
                    //�ȴ��˶���� 
                    m_form_movecontrol.WaitAllMoveFinished();
                    //�л���Դ
                    SetLedLightAndExposure(GlobalVar.gl_List_PointInfo.m_List_PointInfo[n].m_list_zhipingInfo[0]._SubName);
                    //sliderExposureTime.valueChanged(GlobalVar.gl_paras_basler_Exposure_Scan); //ltt
                    baslerCCD1.SetExposureValue(GlobalVar.gl_paras_basler_Exposure_Scan);
                    //������̨�����߳�
                    GlobalVar.gl_List_PointInfo.m_List_PointInfo[n].m_BlockList_ByGroup.CycleDecodeAllBlocks();
                    //��ʼɨ��
                    //richTextBox_SingleShow.AppendText(DateTime.Now.ToString("yyyyMMdd HH:mm:ss") + "\t��ʼɨ������");
                    AddShowLog(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + "\r\n��ʼɨ������", Color.Green);
                    OnePointGroup onegroup = GlobalVar.gl_List_PointInfo.m_List_PointInfo[n];
                    List<SPoint> pointlist = onegroup.m_ListGroup;
                    GlobalVar.gl_CurrentFlowID = GlobalVar.gl_List_PointInfo.m_List_PointInfo[n].FlowID;
                    for (int i = 0; i < pointlist.Count; i++)
                    {
                        if (!GlobalVar.gl_safetyDoor_Front && GlobalVar.gl_usermode == 0) break;
                        //if (GlobalVar.gl_inEmergence) { return; }
                        float dis_X, dis_Y;
                        if (i == 0)
                        {
                            dis_X = (GlobalVar.gl_Ref_Point_CADPos.Pos_X - pointlist[i].Pos_X) * -1; //��еԭ��������,X������Ҫȡ��
                            dis_Y = GlobalVar.gl_Ref_Point_CADPos.Pos_Y - pointlist[i].Pos_Y;

                            //�ο�ԭ��ˮƽλ��ƫ��ֻ�ڵ�һ�μ���
                            dis_X = dis_X + GlobalVar.gl_value_CalibrateDis_X;
                            dis_Y = dis_Y + GlobalVar.gl_value_CalibrateDis_Y;
                        }
                        else
                        {
                            dis_X = (pointlist[i - 1].Pos_X - pointlist[i].Pos_X) * -1; //��еԭ��������,X������Ҫȡ��
                            dis_Y = pointlist[i - 1].Pos_Y - pointlist[i].Pos_Y;
                        }
                        //б�ʵ��µ�λ��ƫ����Ҫÿ�μ���
                        dis_X = dis_X + dis_Y * GlobalVar.gl_value_CalibrateRatio_X;
                        dis_Y = dis_Y + dis_X * GlobalVar.gl_value_CalibrateRatio_Y;

                        m_form_movecontrol.SetPoxEnd_X(dis_X, true);
                        m_form_movecontrol.SetPoxEnd_Y(dis_Y, true);
                        m_form_movecontrol.AxisGroup_Move(true);

                        //�ȴ��˶����
                        m_form_movecontrol.WaitAllMoveFinished();
                        Thread.Sleep(180);  //�������٣����ս���ʱ����Ҫ������ȴ�ʱ�䳤
                        m_current_num++; //
                        baslerCCD1.StartOneShot(); //�˶�������� fortest
                        //m_form_movecontrol.CaptureTrigger(); //����
                        //Thread.Sleep(100);
                        m_wait_picReceived.WaitOne(2000); //���2����û���յ��źţ��Զ���һ��
                    }
                    if (!GlobalVar.gl_safetyDoor_Front && GlobalVar.gl_usermode == 0) break;
                    //�ȴ��˶���� 
                    m_form_movecontrol.WaitAllMoveFinished();
                }
                if (!GlobalVar.gl_safetyDoor_Front && GlobalVar.gl_usermode == 0)
                {
                    m_tag_InCheckAllDecodeFinished = true;
                    m_form_movecontrol_eve_MotionMsg("��ȫ��δ�أ�ֹͣ��ҵ");
                    return;
                }
                //�ȴ��˶���� 
                m_form_movecontrol.WaitAllMoveFinished();
                Thread.Sleep(80);
                //����Ϊ�����ع��ٶ�
                m_form_movecontrol.SetProp_GPSpeed(m_form_movecontrol.m_GPValue_VelHigh_low, m_form_movecontrol.m_GPValue_VelLow_low,
                    m_form_movecontrol.m_GPValue_Acc_low, m_form_movecontrol.m_GPValue_Dec_low);
                m_form_movecontrol.FixPointMotion(0, 0, 3);  //ɨ����ϻ�ԭ��
                m_tag_InCheckAllDecodeFinished = true;
                //�ȴ�ȫ���������
                //richTextBox_SingleShow.AppendText(DateTime.Now.ToString("yyyyMMdd HH:mm:ss") + "\t��ʼ����ȫ��ͼ��");
                AddShowLog(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + "\r\n��ʼ����ȫ��ͼ��", Color.Green);
                cycleCheckAllDecodeFinished();
                //richTextBox_SingleShow.AppendText(DateTime.Now.ToString("yyyyMMdd HH:mm:ss") + "\tȫ��ͼ��������");
                AddShowLog(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + "\r\nȫ��ͼ��������", Color.Green);
                BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (tabControl1.TabPages.Count >= 3)
                        {
                            tabControl1.SelectedIndex = 2;
                            //GC.Collect();
                        }
                    }
                    catch { }
                }));
                Thread.Sleep(80);
                m_form_movecontrol_eve_MotionMsg("�������չ�����ҵ��ϣ��ȴ���һ����ҵ");
            }
            catch (Exception ex)
            {
                logWR.appendNewLogMessage("���������˶����Ƴ��� runCommand error : \r\n" + ex.ToString());
            }
        }

        private void runCommand_withoutCapture(object obj)
        {
            try
            {
                Thread.Sleep(80);
                m_form_movecontrol.WaitAllMoveFinished();
                Thread.Sleep(80);

                for (int n = 0; n < GlobalVar.gl_List_PointInfo.m_List_PointInfo.Count; n++)
                {
                    //ÿ�鿪ʼ���m_current_num
                    m_current_num = 0;
                    //ÿ��ɨ�趼��Ҫ��ԭ��
                    m_form_movecontrol.FixPointMotion(0, 0, 3);
                    //�л���Դ
                    SetLedLightAndExposure(GlobalVar.gl_List_PointInfo.m_List_PointInfo[n].m_list_zhipingInfo[0]._SubName);
                    //��ʼɨ��
                    OnePointGroup onegroup = GlobalVar.gl_List_PointInfo.m_List_PointInfo[n];
                    List<SPoint> pointlist = onegroup.m_ListGroup;
                    for (int i = 0; i < pointlist.Count; i++)
                    {
                        float dis_X, dis_Y;
                        if (i == 0)
                        {
                            dis_X = GlobalVar.gl_Ref_Point_CADPos.Pos_X - pointlist[i].Pos_X;
                            dis_Y = GlobalVar.gl_Ref_Point_CADPos.Pos_Y - pointlist[i].Pos_Y;

                            //�ο�ԭ��ˮƽλ��ƫ��ֻ�ڵ�һ�μ���
                            dis_X = dis_X + GlobalVar.gl_value_CalibrateDis_X;
                            dis_Y = dis_Y + GlobalVar.gl_value_CalibrateDis_Y;
                        }
                        else
                        {
                            dis_X = pointlist[i - 1].Pos_X - pointlist[i].Pos_X;
                            dis_Y = pointlist[i - 1].Pos_Y - pointlist[i].Pos_Y;
                        }
                        //б�ʵ��µ�λ��ƫ����Ҫÿ�μ���
                        dis_X = dis_X + dis_Y * GlobalVar.gl_value_CalibrateRatio_X;
                        dis_Y = dis_Y + dis_X * GlobalVar.gl_value_CalibrateRatio_Y;

                        m_form_movecontrol.SetPoxEnd_X(dis_X, true);
                        m_form_movecontrol.SetPoxEnd_Y(dis_Y, true);
                        m_form_movecontrol.AxisGroup_Move(true);

                        //�ȴ��˶����
                        m_form_movecontrol.WaitAllMoveFinished();
                        Thread.Sleep(40);  //�������٣����ս���ʱ����Ҫ������ȴ�ʱ�䳤
                        m_current_num++; //
                        Thread.Sleep(100);
                    }
                    //�ȴ��˶���� 
                    m_form_movecontrol.WaitAllMoveFinished();
                }
                //�ȴ��˶���� 
                m_form_movecontrol.WaitAllMoveFinished();
                Thread.Sleep(80);
                //����Ϊ�����ع��ٶ�
                m_form_movecontrol.SetProp_GPSpeed(m_form_movecontrol.m_GPValue_VelHigh_low, m_form_movecontrol.m_GPValue_VelLow_low,
                    m_form_movecontrol.m_GPValue_Acc_low, m_form_movecontrol.m_GPValue_Dec_low);
                m_form_movecontrol.FixPointMotion(0, 0, 3);  //ɨ����ϻ�ԭ��
            }
            catch (Exception ex)
            {
                logWR.appendNewLogMessage("���������˶����Ƴ��� runCommand error : \r\n" + ex.ToString());
            }
        }

        protected override void WndProc(ref Message m)
        {
            try
            {
                switch (m.Msg)
                {
                    case GlobalVar.WM_FixedMotion:  //�����˶�
                        try
                        {
                            if (m_form_movecontrol.CheckAxisInMoving())
                            {
                                MessageBox.Show("�豸(��)ΪNot Ready״̬����ֹͣ���������I��", "��ʾ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                return;
                            }
                            string pos_x = Marshal.PtrToStringAnsi(m.WParam);
                            string pos_y = Marshal.PtrToStringAnsi(m.LParam);
                            float dis_X = (GlobalVar.gl_Ref_Point_CADPos.Pos_X - float.Parse(pos_x)) * -1; //��еԭ��������,X������Ҫȡ��
                            float dis_Y = GlobalVar.gl_Ref_Point_CADPos.Pos_Y - float.Parse(pos_y);
                            float x = dis_X + GlobalVar.gl_value_CalibrateDis_X + dis_Y * GlobalVar.gl_value_CalibrateRatio_X;
                            float y = dis_Y + GlobalVar.gl_value_CalibrateDis_Y + dis_X * GlobalVar.gl_value_CalibrateRatio_Y;
                            m_form_movecontrol.FixPointMotion(x, y, 3);
                        }
                        catch (Exception ex)
                        {
                            throw new Exception(ex.ToString());
                        }
                        break;
                }
            }
            catch { }
            base.WndProc(ref m);
        }

        void m_obj_dwg_eve_sendFixMotion(float x, float y)
        {
            if (m_form_movecontrol.CheckAxisInMoving())
            {
                MessageBox.Show("�豸(��)ΪNot Ready״̬����ֹͣ���������I��", "��ʾ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if ((x >= GlobalVar.gl_workArea_width)
                || (y >= GlobalVar.gl_workArea_height))
            {
                MessageBox.Show("�\��Ŀ�˹������������^��Ո�_�J!");
                return;
            }
            m_form_movecontrol.WaitAllMoveFinished();
            Thread.Sleep(200);
            m_form_movecontrol.FixPointMotion(x, y, 3);
        }

        void m_obj_dwg_eve_sendCalPosition(float x, float y)
        {
            if (m_inScanFunction)
            {
                MessageBox.Show("�������I�У������S�M��У�ʄ���!");
            }
            if (m_form_movecontrol.CheckAxisInMoving())
            {
                MessageBox.Show("�豸(��)ΪNot Ready״̬����ֹͣ���������I��", "��ʾ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Thread thread_cal = new Thread(new ThreadStart(delegate
            {
                PositionInfo pos_Mark_Start, pos_Mark_End;
                try
                {
                    //����MARK����Ҫ������ȫ������exposureֵΪ�����趨�е�Ĭ��ֵ
                    SetLedLightAndExposure("MARK");
                    //sliderExposureTime.valueChanged(GlobalVar.gl_paras_basler_Exposure_Calibrate); //ltt
                    baslerCCD1.SetExposureValue(GlobalVar.gl_exposure_Mark_default);
                    //����Ϊ�Ͽ��ٶ�
                    m_form_movecontrol.SetProp_GPSpeed(m_form_movecontrol.m_GPValue_VelHigh_move, m_form_movecontrol.m_GPValue_VelLow_move,
                        m_form_movecontrol.m_GPValue_Acc_move, m_form_movecontrol.m_GPValue_Dec_move);
                    FixPointMotionAndCapture(0.0F, 0.0F, 2, true);  //�؅����c�K����
                    m_bitmap_calibrate_REF = null;
                    DateTime time_start = DateTime.Now;
                    MatrixDecode decoder = new MatrixDecode();
                    for (; ; )
                    {
                        Thread.Sleep(30);
                        TimeSpan ts = DateTime.Now.Subtract(time_start);
                        if (ts.TotalMilliseconds > 3000) { throw new Exception("Y�SУ�ʳ��r!"); }
                        if (m_bitmap_calibrate_REF != null) break;
                    }
                    pos_Mark_Start = decoder.ShapeMatch(m_bitmap_calibrate_REF)[0];

                    ////TO MARK�c
                    m_bitmap_calibrate_END = null;
                    FixPointMotionAndCapture(x, y, 3, false);
                    time_start = DateTime.Now;
                    for (; ; )
                    {
                        TimeSpan ts = DateTime.Now.Subtract(time_start);
                        if (ts.TotalMilliseconds > 3000) { throw new Exception("Y�SУ�ʳ��r!"); }
                        if (m_bitmap_calibrate_END != null) break;
                        Thread.Sleep(30);
                    }
                    pos_Mark_End = decoder.ShapeMatch(m_bitmap_calibrate_END)[0];

                    float ratio_X = GlobalVar.gl_value_MarkPointDiameter * 1.0f / pos_Mark_Start.MCHPatterWidth;
                    float ratio_Y = GlobalVar.gl_value_MarkPointDiameter * 1.0f / pos_Mark_Start.MCHPatterHeight;
                    GlobalVar.gl_value_CalibrateRatio_X = -1.0f * (pos_Mark_End.CenterX - pos_Mark_Start.CenterX) * ratio_X * 1.00F
                        / (GlobalVar.gl_point_CalPos.Pos_Y + (pos_Mark_End.CenterY - pos_Mark_Start.CenterY) * ratio_Y);

                    //GlobalVar.gl_point_CalPos.Pos_X = x; //ltt
                    //GlobalVar.gl_point_CalPos.Pos_Y = y;
                    myfunc.WriteCalPositionInfoToTBS();
                }
                catch { }
            }));
            thread_cal.IsBackground = true;
            thread_cal.Start();
        }

        private void toolStripButton_manualCapture_Click(object sender, EventArgs e)
        {
            //baslerCCD1.StartOneShot(); //�ֶ�����
            //m_form_movecontrol.CaptureTrigger(); //����
        }

        #region  λ��У��
        Thread thread_calibrate;   //��ʼ�պ�У׼�̣߳������⵽����ֹͣ����Ҫ�Դ��߳̽���dispose
        MatrixDecode m_cal_decoder = new MatrixDecode();
        Bitmap m_bitmap_calibrate_REF = null;      //��һ����MARK��ͼƬ
        Bitmap m_bitmap_calibrate_END = null;      //����MARK��ͼƬ   -----Ŀǰ����У׼�ڶ���MARK�㣬Ϊ��ʡʱ��
        private void CalibrateAction()
        {
            //richTextBox_SingleShow.AppendText(DateTime.Now.ToString("yyyyMMdd HH:mm:ss") + "\t��ʼУ׼MARK��");
            AddShowLog(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + "\r\n��ʼУ׼MARK��", Color.Green);
            //sliderExposureTime.valueChanged(GlobalVar.gl_paras_basler_Exposure_Calibrate); //ltt
            baslerCCD1.SetExposureValue(GlobalVar.gl_paras_basler_Exposure_Calibrate);
            m_times_duplicateCalibrate++;
            PositionInfo pos_Mark_Start = new PositionInfo();
            PositionInfo pos_Mark_End = new PositionInfo();
            m_list_bmpReceived.Clear();
            thread_calibrate = new Thread(new ThreadStart(delegate
            {
                bool Mark_I_result = true;   //��һ�����Ƿ�ƥ��OK
                bool Mark_II_result = true;  //�ڶ������Ƿ�ƥ��OK
                DateTime time_start = DateTime.Now;
                try
                {
                    //����MARK����Ҫ������ȫ������exposureֵΪ�����趨�е�Ĭ��ֵ
                    SetLedLightAndExposure("MARK");
                    //sliderExposureTime.valueChanged(GlobalVar.gl_paras_basler_Exposure_Calibrate); //ltt
                    baslerCCD1.SetExposureValue(GlobalVar.gl_exposure_Mark_default);

                    m_form_movecontrol_eve_MotionMsg("MARK����ҽ�����");
                    //if (GlobalVar.gl_inEmergence) { return; }
                    //����Ϊ�Ͽ��ٶ�
                    m_form_movecontrol.SetProp_GPSpeed(m_form_movecontrol.m_GPValue_VelHigh_move, m_form_movecontrol.m_GPValue_VelLow_move,
                        m_form_movecontrol.m_GPValue_Acc_move, m_form_movecontrol.m_GPValue_Dec_move);
                    Thread.Sleep(50);  //��һ�����գ��ȴ�����ⷴӦ�����������٣�
                    FixPointMotionAndCapture(0.0F, 0.0F, 2, true);  //�؅����c�K���� ltt???
                    m_bitmap_calibrate_REF = null;

                    time_start = DateTime.Now;
                    for (; ; )
                    {
                        Thread.Sleep(20);
                        TimeSpan ts = DateTime.Now.Subtract(time_start);
                        if (ts.TotalMilliseconds > 3000) { throw new Exception(); }
                        if (m_bitmap_calibrate_REF != null) { break; }
                    }

                    TimeSpan tss = DateTime.Now.Subtract(timetest);
                    BeginInvoke(new Action(() => { label_test.Text = tss.TotalMilliseconds.ToString(); }));
                    pos_Mark_Start = m_cal_decoder.ShapeMatch(m_bitmap_calibrate_REF)[0];
                    ////BeginInvoke(new Action(() => { m_bitmap_calibrate_REF.Save("c:\\DecodeFailImages\\MARK.BMP"); }));

                    float ratio_X = GlobalVar.gl_value_MarkPointDiameter * 1.0f / pos_Mark_Start.MCHPatterWidth;
                    float ratio_Y = GlobalVar.gl_value_MarkPointDiameter * 1.0f / pos_Mark_Start.MCHPatterHeight;
                    //����ԭ�cƫ��ֵ(attention: λ�D����ϵ�c�S����ϵ�෴A0A0A0A)
                    GlobalVar.gl_value_CalibrateDis_X = (m_bitmap_calibrate_REF.Width / 2 - pos_Mark_Start.CenterX) * ratio_X;
                    GlobalVar.gl_value_CalibrateDis_Y = (pos_Mark_Start.CenterY - m_bitmap_calibrate_REF.Height / 2) * ratio_Y;

                    //TO �ڶ���MARK�c
                    m_bitmap_calibrate_END = null;
                    FixPointMotionAndCapture(GlobalVar.gl_Ref_Point_CADPos.Pos_X - GlobalVar.gl_point_CalPos.Pos_X,
                        GlobalVar.gl_Ref_Point_CADPos.Pos_Y - GlobalVar.gl_point_CalPos.Pos_Y, 3, false); //multiple--7
                    try
                    {
                        time_start = DateTime.Now;
                        for (; ; )
                        {
                            TimeSpan ts = DateTime.Now.Subtract(time_start);
                            if (ts.TotalMilliseconds > 3000) { throw new Exception(); }
                            if (m_bitmap_calibrate_END != null) break;
                            Thread.Sleep(20);
                        }
                        pos_Mark_End = m_cal_decoder.ShapeMatch(m_bitmap_calibrate_END)[0];
                        //�؅����c
                    }
                    catch
                    {
                        Mark_II_result = false;
                    }
                    m_form_movecontrol.WaitAllMoveFinished();
                    Thread.Sleep(30);
                    m_form_movecontrol.FixPointMotion(0, 0, 7);  //�ڶ���MARK��ɨ����ϣ���ԭ��
                    if (Mark_II_result)
                    {
                        //ƫ��б��
                        GlobalVar.gl_value_CalibrateRatio_X = -1.0f * (pos_Mark_End.CenterX - pos_Mark_Start.CenterX) * ratio_X * 1.00F
                            / (GlobalVar.gl_Ref_Point_CADPos.Pos_Y - GlobalVar.gl_point_CalPos.Pos_Y + (pos_Mark_End.CenterY - pos_Mark_Start.CenterY) * ratio_Y);
                        GlobalVar.gl_value_CalibrateRatio_Y = 1.0f * (pos_Mark_End.CenterY - pos_Mark_Start.CenterY) * ratio_Y * 1.00F
                            / (GlobalVar.gl_Ref_Point_CADPos.Pos_X - GlobalVar.gl_point_CalPos.Pos_X + (pos_Mark_End.CenterX - pos_Mark_Start.CenterX) * ratio_X);
                    }
                    BeginInvoke(new Action(() =>
                        {
                            label_deviation_VX.Text = GlobalVar.gl_value_CalibrateDis_X.ToString("0.00000");
                            label_deviation_VY.Text = GlobalVar.gl_value_CalibrateDis_Y.ToString("0.00000");
                            label_deviation_Slopy_X.Text = (GlobalVar.gl_value_CalibrateRatio_X * 10000.00).ToString("0.00000");
                            label_deviation_Slopy_Y.Text = (GlobalVar.gl_value_CalibrateRatio_Y * 10000.00).ToString("0.00000");
                        }));
                }
                catch
                {
                    Mark_I_result = false;
                    ////BeginInvoke(new Action(() => { MessageBox.Show("����У��ʧ����Ո����У��! \r\n" + ex.ToString()); }));
                    //if (m_times_duplicateCalibrate < 5)
                    //{
                    //    m_tag_CalibrateOK = m_inScanFunction = false;
                    //    CalibrateAction();
                    //}
                    //else
                    //{
                    //    //����5��У׼��ʧ���ˣ������˲���
                    //    m_times_duplicateCalibrate = 0;
                    //    GlobalVar.gl_value_CalibrateDis_X = GlobalVar.gl_value_CalibrateDis_Y = 0;
                    //    m_tag_CalibrateOK = true;
                    //    runCommand(null);
                    //}
                }
                m_inScanFunction = false;
                m_tag_CalibrateOK = true;
                m_times_duplicateCalibrate = 0;
                //if (GlobalVar.gl_inEmergence) { return; }
                if (Mark_I_result && Mark_II_result)
                {
                    AddShowLog(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + "\r\nУ׼MARK��ɹ�", Color.Green);
                    runCommand(null);
                    m_times_duplicateCalibrate = 0;
                    //richTextBox_SingleShow.AppendText(DateTime.Now.ToString("yyyyMMdd HH:mm:ss") + "\tУ׼MARK��ɹ�");

                }
                else
                {
                    updateLedLightStatus(2);
                    ShowPsdErrForm sp = new ShowPsdErrForm("����У��ʧ�����{���uƷλ���������I!", false);
                    sp.ShowDialog();
                    updateLedLightStatus(1);
                    m_form_movecontrol.Stage2ZaibanPass();
                    //richTextBox_SingleShow.AppendText(DateTime.Now.ToString("yyyyMMdd HH:mm:ss") + "\t�����ذ��˳�");
                    AddShowLog(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + "\r\n�����ذ��˳�", Color.Green);
                    m_count_BoardIn--;
                    updateLedLightStatus(0);
                    clearTags();
                    //richTextBox_SingleShow.AppendText(DateTime.Now.ToString("yyyyMMdd HH:mm:ss") + "\tУ׼MARK��ʧ��");
                    AddShowLog(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + "\r\nУ׼MARK��ʧ��", Color.Green);
                }
            }));
            thread_calibrate.IsBackground = true;
            thread_calibrate.Start();
        }



        private void test_CalibrateAction()
        {
            //richTextBox_SingleShow.AppendText(DateTime.Now.ToString("yyyyMMdd HH:mm:ss") + "\t��ʼУ׼MARK��");
            AddShowLog(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + "\r\n��ʼУ׼MARK��", Color.Green);
            //sliderExposureTime.valueChanged(GlobalVar.gl_paras_basler_Exposure_Calibrate); //ltt
            //baslerCCD1.SetExposureValue(GlobalVar.gl_paras_basler_Exposure_Calibrate);
            //m_times_duplicateCalibrate++;
            PositionInfo pos_Mark_Start = new PositionInfo();
            PositionInfo pos_Mark_End = new PositionInfo();
            m_list_bmpReceived.Clear();
            thread_calibrate = new Thread(new ThreadStart(delegate
            {
                bool Mark_I_result = true;   //��һ�����Ƿ�ƥ��OK
                bool Mark_II_result = true;  //�ڶ������Ƿ�ƥ��OK
                DateTime time_start = DateTime.Now;
                try
                {
                    //����MARK����Ҫ������ȫ������exposureֵΪ�����趨�е�Ĭ��ֵ
                    //SetLedLightAndExposure("MARK");
                    //sliderExposureTime.valueChanged(GlobalVar.gl_paras_basler_Exposure_Calibrate); //ltt
                    //baslerCCD1.SetExposureValue(GlobalVar.gl_exposure_Mark_default);

                    m_form_movecontrol_eve_MotionMsg("MARK����ҽ�����");
                    //if (GlobalVar.gl_inEmergence) { return; }
                    //����Ϊ�Ͽ��ٶ�
                    //m_form_movecontrol.SetProp_GPSpeed(m_form_movecontrol.m_GPValue_VelHigh_move, m_form_movecontrol.m_GPValue_VelLow_move,
                    // m_form_movecontrol.m_GPValue_Acc_move, m_form_movecontrol.m_GPValue_Dec_move);
                    // Thread.Sleep(50);  //��һ�����գ��ȴ�����ⷴӦ�����������٣�
                    //FixPointMotionAndCapture(0.0F, 0.0F, 2, true);  //�؅����c�K���� ltt???
                    //m_bitmap_calibrate_REF = null;

                    time_start = DateTime.Now;
                    //for (; ; )
                    //{
                    //    Thread.Sleep(20);
                    //    TimeSpan ts = DateTime.Now.Subtract(time_start);
                    //    if (ts.TotalMilliseconds > 3000) { throw new Exception(); }
                    //    if (m_bitmap_calibrate_REF != null) { break; }
                    //}

                    TimeSpan tss = DateTime.Now.Subtract(timetest);
                    BeginInvoke(new Action(() => { label_test.Text = tss.TotalMilliseconds.ToString(); }));
                    //pos_Mark_Start = m_cal_decoder.ShapeMatch(m_bitmap_calibrate_REF)[0];
                    ////BeginInvoke(new Action(() => { m_bitmap_calibrate_REF.Save("c:\\DecodeFailImages\\MARK.BMP"); }));

                    float ratio_X = 1;// GlobalVar.gl_value_MarkPointDiameter * 1.0f / pos_Mark_Start.MCHPatterWidth;
                    float ratio_Y = 1;// GlobalVar.gl_value_MarkPointDiameter * 1.0f / pos_Mark_Start.MCHPatterHeight;
                    //����ԭ�cƫ��ֵ(attention: λ�D����ϵ�c�S����ϵ�෴A0A0A0A)
                    GlobalVar.gl_value_CalibrateDis_X = 0;// (m_bitmap_calibrate_REF.Width / 2 - pos_Mark_Start.CenterX) * ratio_X;
                    GlobalVar.gl_value_CalibrateDis_Y = 0;// (pos_Mark_Start.CenterY - m_bitmap_calibrate_REF.Height / 2) * ratio_Y;

                    //TO �ڶ���MARK�c
                    m_bitmap_calibrate_END = null;
                    //FixPointMotionAndCapture(GlobalVar.gl_Ref_Point_CADPos.Pos_X - GlobalVar.gl_point_CalPos.Pos_X,
                    //    GlobalVar.gl_Ref_Point_CADPos.Pos_Y - GlobalVar.gl_point_CalPos.Pos_Y, 3, false); //multiple--7
                    try
                    {
                        time_start = DateTime.Now;
                        //for (; ; )
                        //{
                        //    TimeSpan ts = DateTime.Now.Subtract(time_start);
                        //    if (ts.TotalMilliseconds > 3000) { throw new Exception(); }
                        //    if (m_bitmap_calibrate_END != null) break;
                        //    Thread.Sleep(20);
                        //}
                        //pos_Mark_End = m_cal_decoder.ShapeMatch(m_bitmap_calibrate_END)[0];
                        //�؅����c
                    }
                    catch
                    {
                        Mark_II_result = false;
                    }
                    //m_form_movecontrol.WaitAllMoveFinished();
                    Thread.Sleep(30);
                    // m_form_movecontrol.FixPointMotion(0, 0, 7);  //�ڶ���MARK��ɨ����ϣ���ԭ��
                    if (Mark_II_result)
                    {
                        //ƫ��б��
                        GlobalVar.gl_value_CalibrateRatio_X = 0;// -1.0f * (pos_Mark_End.CenterX - pos_Mark_Start.CenterX) * ratio_X * 1.00F
                                                                /// (GlobalVar.gl_Ref_Point_CADPos.Pos_Y - GlobalVar.gl_point_CalPos.Pos_Y + (pos_Mark_End.CenterY - pos_Mark_Start.CenterY) * ratio_Y);
                        GlobalVar.gl_value_CalibrateRatio_Y = 0;// 1.0f * (pos_Mark_End.CenterY - pos_Mark_Start.CenterY) * ratio_Y * 1.00F
                                                                // / (GlobalVar.gl_Ref_Point_CADPos.Pos_X - GlobalVar.gl_point_CalPos.Pos_X + (pos_Mark_End.CenterX - pos_Mark_Start.CenterX) * ratio_X);
                    }
                    BeginInvoke(new Action(() =>
                    {
                        label_deviation_VX.Text = GlobalVar.gl_value_CalibrateDis_X.ToString("0.00000");
                        label_deviation_VY.Text = GlobalVar.gl_value_CalibrateDis_Y.ToString("0.00000");
                        label_deviation_Slopy_X.Text = (GlobalVar.gl_value_CalibrateRatio_X * 10000.00).ToString("0.00000");
                        label_deviation_Slopy_Y.Text = (GlobalVar.gl_value_CalibrateRatio_Y * 10000.00).ToString("0.00000");
                    }));
                }
                catch
                {
                    Mark_I_result = false;
                }
                m_inScanFunction = false;
                m_tag_CalibrateOK = true;
                m_times_duplicateCalibrate = 0;
                if (Mark_I_result && Mark_II_result)
                {
                    runCommand(null);
                    m_times_duplicateCalibrate = 0;
                }
                else
                {
                    updateLedLightStatus(2);
                    ShowPsdErrForm sp = new ShowPsdErrForm("����У��ʧ�����{���uƷλ���������I!", false);
                    sp.ShowDialog();
                    updateLedLightStatus(1);
                    m_form_movecontrol.Stage2ZaibanPass();
                    AddShowLog(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + "\r\n�����ذ��˳�", Color.Green);
                    m_count_BoardIn--;
                    updateLedLightStatus(0);
                    clearTags();
                    AddShowLog(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + "\r\nУ׼MARK��ʧ��", Color.Green);
                }
            }));
            thread_calibrate.IsBackground = true;
            thread_calibrate.Start();
        }
        /* �`����xУ�ʷ�ʽ����Ҫÿ�Β�3���c ������
        private void CalibrateAction()
        {
            PositionInfo pos_REF, pos_X;
            Thread thread_cal = new Thread(new ThreadStart(delegate
            {
                try
                {
                    MatrixDecode decoder = new MatrixDecode();
                    if (GlobalVar.gl_List_PointInfo.Count == 0)
                    {
                        MessageBox.Show("δ����ƷĿCAD�ęn��Ո�_�J!");
                        return;
                    }
                    FixPointMotionAndCapture(0.0F, 0.0F);  //�؅����c�K����
                    m_bitmap_calibrate_REF = null;
                    for (; ; )
                    { if (m_bitmap_calibrate_REF != null)break; Thread.Sleep(100); }
                    pos_REF = decoder.ShapeMatch(m_bitmap_calibrate_REF)[0];
                    //TO MARK_X�c
                    m_bitmap_calibrate_X = null;
                    FixPointMotionAndCapture(GlobalVar.gl_Ref_Point.Pos_X - GlobalVar.gl_point_CalibrationPoint_X.Pos_X
                        , GlobalVar.gl_Ref_Point.Pos_Y - GlobalVar.gl_point_CalibrationPoint_X.Pos_Y);
                    for (; ; )
                    { if (m_bitmap_calibrate_X != null)break; Thread.Sleep(100); }
                    pos_X = decoder.ShapeMatch(m_bitmap_calibrate_X)[0];
                    //�؅����c
                    FixPointMotion(0.0F, 0.0F);  
                    float ratio_X = GlobalVar.gl_value_BMRadio / pos_REF.MCHPatterWidth;
                    float ratio_Y = GlobalVar.gl_value_BMRadio / pos_REF.MCHPatterHeight;
                    GlobalVar.gl_value_CalibrateDis_X = (pos_REF.CenterX - m_bitmap_calibrate_X.Width / 2) * ratio_X;
                    GlobalVar.gl_value_CalibrateDis_Y = (pos_REF.CenterY - m_bitmap_calibrate_X.Height / 2) * ratio_Y;
                    GlobalVar.gl_value_CalibrateRatio_X = (pos_X.CenterY - pos_REF.CenterY) * ratio_Y * 1.00F
                        / (GlobalVar.gl_Ref_Point.Pos_X - GlobalVar.gl_point_CalibrationPoint_X.Pos_X
                        + (pos_X.CenterX - pos_REF.CenterX) * ratio_X);
                }
                catch(Exception  ex)
                { BeginInvoke(new Action(() => { MessageBox.Show("����У��ʧ����Ո����У��! \r\n" + ex.ToString()); })); }
            }));
            thread_cal.IsBackground = true;
            thread_cal.Start();
        }

        //����Y��������
        private void CalibrateAction_Y()
        {
            PositionInfo pos_REF,  pos_Y;
            Thread thread_cal = new Thread(new ThreadStart(delegate
            {
                try
                {
                    MatrixDecode decoder = new MatrixDecode();
                    if (GlobalVar.gl_List_PointInfo.Count == 0)
                    {
                        MessageBox.Show("δ����ƷĿCAD�ęn��Ո�_�J!");
                        return;
                    }
                    FixPointMotionAndCapture(0.0F, 0.0F);  //�؅����c�K����
                    m_bitmap_calibrate_REF = null;
                    for (; ; )
                    { if (m_bitmap_calibrate_REF != null)break; Thread.Sleep(100); }
                    pos_REF = decoder.ShapeMatch(m_bitmap_calibrate_REF)[0];
                    //TO MARK_Y�c
                    m_bitmap_calibrate_Y = null;
                    FixPointMotionAndCapture(GlobalVar.gl_Ref_Point.Pos_X - GlobalVar.gl_point_CalibrationPoint_Y.Pos_X
                        , GlobalVar.gl_Ref_Point.Pos_Y - GlobalVar.gl_point_CalibrationPoint_Y.Pos_Y);
                    for (; ; )
                    { if (m_bitmap_calibrate_Y != null)break; Thread.Sleep(100); }
                    pos_Y = decoder.ShapeMatch(m_bitmap_calibrate_Y)[0];
                    //�؅����c
                    FixPointMotion(0.0F, 0.0F);  
                    float ratio_X = GlobalVar.gl_value_BMRadio / pos_REF.MCHPatterWidth;
                    float ratio_Y = GlobalVar.gl_value_BMRadio / pos_REF.MCHPatterHeight;
                    GlobalVar.gl_value_CalibrateRatio_Y = (pos_Y.CenterX - pos_REF.CenterX) * ratio_X * 1.00F
                        / ((GlobalVar.gl_Ref_Point.Pos_Y - GlobalVar.gl_point_CalibrationPoint_Y.Pos_Y)
                        + (pos_Y.CenterY - pos_REF.CenterY) * ratio_Y);
                }
                catch (Exception ex)
                { BeginInvoke(new Action(() => { MessageBox.Show("����У��ʧ����Ո����У��! \r\n" + ex.ToString()); })); }
            }));
            thread_cal.IsBackground = true;
            thread_cal.Start();
        }
        */

        private void PositionCalibrate(Bitmap bmp)
        {

        }

        private void pictureBox_capture_Paint(object sender, PaintEventArgs e)
        {
        }
        #endregion

        private void OneCircleReset()
        {
            try
            {
                m_current_num = 0;
                m_list_bmpReceived.Clear();

                //�����λDetailBlock�еı����Ͳ�����
                for (int i = 0; i < GlobalVar.gl_List_PointInfo.m_List_PointInfo.Count; i++)
                {
                    OneGroup_Blocks onegroupBlock = GlobalVar.gl_List_PointInfo.m_List_PointInfo[i].m_BlockList_ByGroup;
                    for (int m = 0; m < onegroupBlock.m_BlockinfoList.Count; m++)
                    {
                        onegroupBlock.m_BlockinfoList[m].Reset();
                    }
                }
                BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (tabControl1.TabPages.Count >= 3)
                        {
                            tabControl1.SelectedIndex = 1;
                        }
                    }
                    catch { }
                }));
            }
            catch { }
        }

        private void clearTags()
        {
            m_tag_CalibrateOK = false;
            m_tag_InCheckAllDecodeFinished = false;
            m_tag_DBQueryFinished = false;
            m_tag_ShifttoStage2Checked = false;
            m_coilstatus_ShiftToStage2 = false;
            m_inScanFunction = false;
            //m_coilstatus_MachineMoveFinished = true;
            //Thread.Sleep(1000);
            //m_coilstatus_MachineMoveFinished = false;
        }

        private void ToolStripMenuItem_save_Click(object sender, EventArgs e)
        {
            if (m_bitmap == null)
            {
                MessageBox.Show("�DƬ��գ��惦�oЧ!");
                return;
            }
            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                m_bitmap.Save(saveFileDialog1.FileName);
            }
        }

        private void btn_softReset_Click(object sender, EventArgs e)
        {
            clearTags();
            m_manualCycleReset = true;
            m_count_BoardIn = 0;
            //OneCircleReset();
        }

        private void timer_alarm_Tick(object sender, EventArgs e)
        {
            try
            {
                if (button_alarm.BackColor == Color.Gray)
                {
                    button_alarm.BackColor = Color.Red;
                }
                else
                {
                    button_alarm.BackColor = Color.Gray;
                }
                button_alarm.Refresh();
            }
            catch { }
        }

        private void ToolStripMenuItem_paraSetting_Click(object sender, EventArgs e)
        {
            para_Setting();
        }

        private void para_Setting()
        {
            try
            {
                string _old_ScanPort = GlobalVar.gl_serialPort_Scan;
                Parameters para = new Parameters(this);
                if (para.ShowDialog() == DialogResult.OK)
                {
                    if (GlobalVar.gl_ProductModel == "")
                    {
                        MessageBox.Show("��ȷ��������Ϣ");
                        return;
                    }
                    UpdateParaSetting(para);
                    myfunc.WriteGlobalInfoToTBS();
                    if (_old_ScanPort != GlobalVar.gl_serialPort_Scan)
                    {
                        //���´��_����
                        openScanPort();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("�����惦ʧ��: " + ex.ToString());
            }
        }

        private void UpdateParaSetting(Parameters para)
        {
            try
            {
                //���P����
                GlobalVar.gl_OneSheetCount = para._TotalSheetPcs;
                //�ظ���������
                GlobalVar.gl_decode_times = para._RedecodeTimes;
                //���r�r�L
                GlobalVar.gl_decode_timeout = para._decode_timeOut;
                //ƥ���
                GlobalVar.gl_MinMatchScore = para._MatchMinScore;
                //�l�a�L��
                //GlobalVar.gl_length_PCSBarcodeLength = para._BarcodeLength;
                GlobalVar.gl_length_sheetBarcodeLength = para._SheetBarcodeLength;
                //��ʾ��ߴ�
                GlobalVar.block_width = para._BlockWidth;
                GlobalVar.block_heigt = para._BlockHeight;
                //Mark�cֱ��
                try
                { GlobalVar.gl_value_MarkPointDiameter = float.Parse(para._MarkPointDiameter); }
                catch { GlobalVar.gl_value_MarkPointDiameter = 1; }
                //�Y���ς������
                //GlobalVar.gl_path_FileResult = para._Path_ResultFileSave;
                GlobalVar.gl_path_FileBackUp = para._Path_ResultFileBackUp;
                //ͼƬ�洢
                GlobalVar.gl_PicsSavePath = para._PicSavePath;
                GlobalVar.gl_saveCapturePics = para._SaveCapturePics;
                GlobalVar.gl_NGPicsSavePath = para._NGPicsSavePath;
                GlobalVar.gl_saveDecodeFailPics = para._SaveNGPics;
                //����ѡ��
                GlobalVar.gl_serialPort_Scan = para._BarcodeScanPort;
                //���������λ
                GlobalVar.gl_PosLimit_X_P = para._PosLimit_X_P;
                GlobalVar.gl_PosLimit_X_N = para._PosLimit_X_N;
                GlobalVar.gl_PosLimit_Y_P = para._PosLimit_Y_P;
                GlobalVar.gl_PosLimit_Y_N = para._PosLimit_Y_N;
                //�ع�ֵ
                GlobalVar.gl_exposure_Mark_default = para._Exposure_Mark_Default;
                GlobalVar.gl_exposure_Matrix_default = para._Exposure_Matrix_Default;

                GlobalVar.gl_exposure_Mark_Geortek = para._Exposure_Mark_GER;
                GlobalVar.gl_exposure_Matrix_Geortek = para._Exposure_Matrix_GER;
                GlobalVar.gl_exposure_Mark_ST = para._Exposure_Mark_ST;
                GlobalVar.gl_exposure_Matrix_ST = para._Exposure_Matrix_ST;
                GlobalVar.gl_exposure_Mark_AAC = para._Exposure_Mark_AAC;
                GlobalVar.gl_exposure_Matrix_AAC = para._Exposure_Matrix_AAC;
                GlobalVar.gl_exposure_Mark_Knowles = para._Exposure_Mark_KNOWLES;
                GlobalVar.gl_exposure_Matrix_Knowles = para._Exposure_Matrix_KNOWLES;
                //�ع�ֵNEW
                GlobalVar.gl_Model_prodcutTypeMic = para._Model_ProductType;
                GlobalVar.gl_Model_exposure = para._Model_ExposureMic;
                GlobalVar.gl_Model_prodcutTypeProx = para._Model_ProductTypeProx;
                GlobalVar.gl_Model_exposureProx = para._Model_ExposureProx;
                GlobalVar.gl_Model_exposurePcs = para._Model_ExposurePcs;
                GlobalVar.gl_Model_exposureIC = para._Model_ExposureIC;
                //ʹ��Halcon����
                GlobalVar.gl_bUseHalcon = para._UseHalcon;
            }
            catch
            { }
        }

        private void ToolStripMenuItem_exit_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("�_���P�]ܛ����", "��ʾ", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1)
                == System.Windows.Forms.DialogResult.Yes)
            {
                // ɾ��ԭ���ļ��� [11/10/2017 617004]
                //                 if (Application.StartupPath!=GlobalVar.gl_strAppPath)
                //                 {
                //                     Directory.Delete(Application.StartupPath);
                //                 }
                Application.Exit();
            }
        }

        private void button_clearTestinfo_Click(object sender, EventArgs e)
        {
            GlobalVar.gl_testinfo_decodefailed = GlobalVar.gl_testinfo_totalSheet
                = GlobalVar.gl_testinfo_totalTest = 0;
            updatetestinfo();
        }

        private void ToolStripMenuItem_normalWorkMode_Click(object sender, EventArgs e)
        {
            SetRailWorkMode(0);
            EmergenceReset();
        }

        private void ToolStripMenuItem_passOnlyMode_Click(object sender, EventArgs e)
        {
            SetRailWorkMode(1);
            EmergenceReset();
        }

        #region LOT�����ѯ
        private void initTestInfo()
        {
            try
            {
                //textBox_MPN.Text = GlobalVar.gl_str_MPN;
                textBox_LotNo.Text = GlobalVar.gl_str_LotNo;
                //textBox_qualifiedNo_OQC.Text = GlobalVar.gl_str_QualifiedNo;
            }
            catch { }
        }

        private void textBox_LotNo_KeyPress(object sender, KeyPressEventArgs e)
        {
            //if (e.KeyChar == 13)
            //{
            //    if (!queryLotNoInfo()) return;
            //    if (!getMICInfobyLotNo()) return;
            //    NewPartNoLoad();
            //}
        }

        private void button_OK_FT_Click(object sender, EventArgs e)
        {
            m_count_BoardIn = 0;
            autoDeleteOldPic();
            if (txtbox_DeviceID.Text == "")
            {
                MessageBox.Show("�������豸���");
                return;
            }
            else
            {
                string iniFilePath = GlobalVar.gl_strTargetPath + "\\" + GlobalVar.gl_iniTBS_FileName;
                myfunc.CheckFileExit();
                MyFunctions.WritePrivateProfileString(GlobalVar.gl_inisection_Global, GlobalVar.gl_iniKey_strDeviceID, GlobalVar.gl_DeviceID, iniFilePath);     //�豸���
            }
            if (GlobalVar.gl_Board1245EInit && m_form_movecontrol.CheckAxisInMoving())
            {
                MessageBox.Show("�豸(��)ΪNot Ready״̬����ֹͣ���������I��", "��ʾ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Thread.Sleep(100);
            m_form_movecontrol.ResetAlarmError(); //��λ�忨�쳣״̬
            Thread.Sleep(100);
            if (!queryLotNoInfo()) return;
            addLogStr("LOT:" + textBox_LotNo.Text + "\r\n�ο�ԭ��X����:" + textBox_fixPoint_x.Text + "\r\n�ο�ԭ��Y����:" + textBox_fixPoint_y.Text + "\r\n�ο�ԭ��Z����:" + textBox_fixPoint_z.Text + "" + textBox_CalPos_X.Text + "" + textBox_CalPos_Y.Text + "\r\n��ԭ��Z����:" + textBox_CalPos_Z.Text);// ������־ ��¼LOT [10/26/2017 617004]
            //��Ҫ�Ȳ�ѯ������Ϣ�����CAD��Ϣ��FLOWID���ܲ�ѯMIC/PROX��Ϣ
            if (!GlobalVar.gl_AutoLoadType || textBox_LotNo.Text == "99999999999")
            {
                NewPartNoLoad();
            }
            else
            {
                //if (!CommenConfigLoad())
                //{
                NewPartNoNetLoad();
                //   }
            }

            if (GlobalVar.gl_LinkType == LinkType.MIC)
            {
                if (!getMICInfobyLotNo()) return;
            }
            else if (GlobalVar.gl_LinkType == LinkType.PROX)
            {
                if (!getProxInfobyLotNo()) return;
            }
            else if (GlobalVar.gl_LinkType == LinkType.BARCODE)
            {
                if (!getPcsInfobyLotNo()) return;
                //if (CheckShtBarcode(GlobalVar.gl_strShtBarcode)) return;
            }
            else if (GlobalVar.gl_LinkType == LinkType.IC)
            {
                for (int i = 0; i < GlobalVar.gl_List_PointInfo.m_List_PointInfo.Count; i++)
                {
                    OnePointGroup onegroup = GlobalVar.gl_List_PointInfo.m_List_PointInfo[i];
                    onegroup.m_list_zhipingInfo.Clear();
                    //FOR TEST
                    ZhiPinInfo mi = new ZhiPinInfo();
                    mi._SubName = "IC";
                    onegroup.m_list_zhipingInfo.Add(mi);

                }
            }
            else
            {
                MessageBox.Show("δ֪������ҵ���ͣ�", "��ʾ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;


            }
            try
            {
                if (GlobalVar.gl_LinkType == LinkType.IC)
                {
                    GlobalVar.gl_ProductType = new string[] { "IC" };
                }
                else
                {
                    this.Invoke(new Action(() =>
                    {
                        List<string> listStr = new List<string>();
                        string strs = "";
                        for (int i = 0; i < GlobalVar.gl_List_PointInfo.m_List_PointInfo.Count; i++)
                        {
                            OnePointGroup onegroup = GlobalVar.gl_List_PointInfo.m_List_PointInfo[i];
                            for (int j = 0; j < onegroup.m_list_zhipingInfo.Count; j++)
                            {
                                ZhiPinInfo zhipin = onegroup.m_list_zhipingInfo[j];
                                string str = zhipin._SubName;
                                if (!listStr.Contains(str))
                                {
                                    listStr.Add(str);
                                    strs += str + ",";
                                }
                            }
                        }
                        GlobalVar.gl_ProductType = listStr.ToArray();
                        tsslB_productType.Text = "��Ʒ����:[" + strs.Trim(',') + "]";
                    }));
                }
            }
            catch { }
            //�ο�У׼Mark���ȡ
            //MoveToAxisZRef(); //Z��2017.12.18            
            myfunc.ReadMarkDefault();
            SetCalPosValue(GlobalVar.gl_point_CalPos);
            #region ��ʾBLOCK����ʼ������BLOCK list�����Ϣ
            GlobalVar.gl_totalCount = 0;
            for (int m = 0; m < GlobalVar.gl_List_PointInfo.m_List_PointInfo.Count; m++)
            {

                List<SPoint> onegroupPoint = GlobalVar.gl_List_PointInfo.m_List_PointInfo[m].m_ListGroup;
                List<DetailBlock> blocklist = GlobalVar.gl_List_PointInfo.m_List_PointInfo[m].m_BlockList_ByGroup.m_BlockinfoList;
                List<OBJ_TipPoint> m_TipPoint_List = new List<OBJ_TipPoint>();
                GlobalVar.gl_List_PointInfo.m_List_PointInfo[m].m_BlockList_ByGroup.FlowID
                    = GlobalVar.gl_List_PointInfo.m_List_PointInfo[m].FlowID;
                //���ղ�ͬFLOWID�ֵ�BLOCK�飬ÿ��n��BLOCK
                //OneGroup_Blocks blockGroup = new OneGroup_Blocks();
                //OneGroup_Blocks blockGroup = GlobalVar.gl_List_PointInfo.m_List_PointInfo[m].m_BlockList_ByGroup;
                try
                {
                    //blockGroup = new OneGroup_Blocks();
                    //blockGroup.FlowID = GlobalVar.gl_List_PointInfo.m_List_PointInfo[m].FlowID;
                    tabPage_mainview.Controls.Clear();
                    for (int i = 0; i < onegroupPoint.Count; i++)
                    {
                        DetailBlock bi = new DetailBlock();
                        bi.flowid = GlobalVar.gl_List_PointInfo.m_List_PointInfo[m].FlowID;
                        bi.Pos_X_CAD = Math.Abs(onegroupPoint[i].Pos_X);
                        bi.Pos_Y_CAD = Math.Abs(onegroupPoint[i].Pos_Y);
                        bi.Pos_Z_CAD = Math.Abs(onegroupPoint[i].Pos_Z);
                        bi.m_PcsNo = onegroupPoint[i].PointNumber;
                        bi.m_PcsNo_Mapping = GetMapNum(bi.m_PcsNo);
                        bi.Location = newPointConvert(bi, i);
                        bi.Width = GlobalVar.block_width;
                        bi.Height = GlobalVar.block_heigt;
                        bi.setPositionDisplay((Math.Abs(onegroupPoint[i].Pos_X - GlobalVar.gl_Ref_Point_CADPos.Pos_X).ToString("0.00"))
                            , (Math.Abs(onegroupPoint[i].Pos_Y - GlobalVar.gl_Ref_Point_CADPos.Pos_Y)).ToString("0.00"));
                        bi.Parent = tabPage_mainview;
                        blocklist.Add(bi);

                        #region ������ʾCAD������Ϣ
                        //  [10/20/2017 617004]
                        OBJ_TipPoint TP = new OBJ_TipPoint();
                        //TP._tipIndex = ((SPoint)GlobalVal.gl_List_BlockInfo[i]).PointNumber.ToString();
                        TP.Name = "OBJ_TipPoint" + onegroupPoint[i].PointNumber.ToString("00");
                        TP._tipIndex = onegroupPoint[i].Point_name;
                        TP._Pos_X = onegroupPoint[i].Pos_X.ToString("0.00");
                        TP._Pos_Y = onegroupPoint[i].Pos_Y.ToString("0.00");
                        TP._TPSequence = onegroupPoint[i].PointNumber.ToString();
                        TP._AngleValue = onegroupPoint[i].Angle_deflection.ToString();
                        TP._LineSequence = onegroupPoint[i].Line_sequence.ToString();
                        m_TipPoint_List.Add(TP);
                        Obj_TipPoint_byTPName _comparer = new Obj_TipPoint_byTPName();
                        m_TipPoint_List.Sort(_comparer);
                        #endregion
                    }
                    for (int i = m_TipPoint_List.Count - 1; i >= 0; i--)
                    {
                        m_TipPoint_List[i].Parent = TipPointShow;
                        m_TipPoint_List[i].Dock = DockStyle.Top;
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception("��ʼ��gl_List_BlockInfo����" + ex.ToString());
                }
                GlobalVar.gl_totalCount += blocklist.Count;
                init_tabPage_mainview();
            }
            #endregion
        }

        private void MoveToAxisZRef()
        {
            if (GlobalVar.gl_LinkType != GlobalVar.gl_LastLinkType)
            {
                string strconfile = Application.StartupPath + "\\config.ini";
                MyFunctions.WritePrivateProfileString(GlobalVar.gl_iniSection_AxisZRef, GlobalVar.gl_inikey_lastLinkType, GlobalVar.gl_LastLinkType.ToString(), strconfile);
                m_form_movecontrol.MoveToAxisZRef(GlobalVar.gl_dAxisZRef);
            }
        }

        private bool queryLotNoInfo()
        {
            try
            {
                string lotno = textBox_LotNo.Text.Trim();
                if (textBox_operator.Text.Trim().Length == 0)
                {
                    MessageBox.Show("���I�T��̖ݔ���գ�Ո�_�J��", "��ʾ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }
                if (lotno.Length != 11)
                {
                    MessageBox.Show("LOT̖�l�a�L�Ȳ�����Ո�_�J��", "��ʾ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }
                if (!myfunc.checkStringIsLegal(lotno, 1))
                {
                    MessageBox.Show("LotNoݔ�벻�Ϸ���", "��ʾ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    //RecoverTestInfoInput();
                    textBox_LotNo.SelectAll();
                    textBox_LotNo.Focus();
                    return false;
                }
                else
                {
                    if (m_DBQuery.setDataBaseMessage(lotno))
                    {
                        GlobalVar.gl_str_LotNo = lotno;
                        textBox_MPN.Text = GlobalVar.gl_str_Product;
                        button_partName.Text = GlobalVar.gl_ProductModel;

                        //PCS����汾  [10/18/2017 617004]
                        GlobalVar.gl_DBquery = new DBQuery();
                        GlobalVar.gl_bPcsManage = GlobalVar.gl_DBquery.CheckPcsManage(GlobalVar.gl_ProductModel);
                        //if (GlobalVar.gl_bPcsManage)
                        //{
                        //    tssl_PCSManage.Text = "PCS����汾";
                        //    tssl_PCSManage.BackColor = Color.Orange;
                        //}
                        //else
                        //{
                        //    tssl_PCSManage.Text = "";
                        //    tssl_PCSManage.BackColor = Color.Transparent;
                        //}
                    }
                    button_OK_FT.Focus();
                    string iniFilePath = GlobalVar.gl_strTargetPath + "\\" + GlobalVar.gl_iniTBS_FileName;
                    MyFunctions.WritePrivateProfileString(GlobalVar.gl_inisection_TestInfo, GlobalVar.gl_iniKey_LotNo, GlobalVar.gl_str_LotNo, iniFilePath);
                }
                #region ƷĿ�ϲ�����2017.07.04
                GlobalVar.gl_strMpnAssemble = GlobalVar.gl_strAssemble = GlobalVar.gl_strAssembleX = "";
                GlobalVar.gl_bMPNPlan = m_DBQuery.CheckMPNPlan(GlobalVar.gl_ProductModel);
                if (GlobalVar.gl_bMPNPlan)
                {
                    GlobalVar.gl_strAssemble = DBQuery.GetAssemble(GlobalVar.gl_str_LotNo);//ֻ���ƷĿ�ϲ�β��
                    GlobalVar.gl_listMPNPlan = DBQuery.GetAssembleX(GlobalVar.gl_str_LotNo);//���ƷĿ�ϲ��ļ�����-��ϣ�E75��*��ʾ��KK*����2016.08
                    if (GlobalVar.gl_listMPNPlan == null || GlobalVar.gl_listMPNPlan.Count == 0 || GlobalVar.gl_strAssemble == "")
                    {
                        MessageBox.Show("ƷĿ�ϲ�����\rδ�ҵ���Ϲ���");
                        return false;
                    }
                    for (int i = 0; i < GlobalVar.gl_listMPNPlan.Count; i++)
                    {
                        string str = GlobalVar.gl_listMPNPlan[i].Position + GlobalVar.gl_listMPNPlan[i].Code + GlobalVar.gl_listMPNPlan[i].Flowid.ToString("00");
                        GlobalVar.gl_strMpnAssemble += str + "-";
                        GlobalVar.gl_strAssembleX += GlobalVar.gl_listMPNPlan[i].Code;
                    }
                }
                #endregion
                return true;
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString());
                return false;
            }
        }

        //MIC������Ϣ��ȡ
        public bool getMICInfobyLotNo()
        {
            try
            {
                for (int i = 0; i < GlobalVar.gl_List_PointInfo.m_List_PointInfo.Count; i++)
                {
                    OnePointGroup onegroup = GlobalVar.gl_List_PointInfo.m_List_PointInfo[i];
                    onegroup.m_list_zhipingInfo.Clear();
                    //GlobalVar.gl_list_ZhiPinInfo.Clear();
                    if ((GlobalVar.gl_str_LotNo == "99999999999")
                        || (GlobalVar.gl_str_LotNo == "99999999998"))
                    {
                        //FOR TEST
                        ZhiPinInfo mi = new ZhiPinInfo();
                        mi._SubName = "KNOWLES";
                        mi._HeadStr = "FN8";
                        mi._BarcodeLength = 16;
                        mi._StartPos = 0;
                        mi._StartLen = 3;
                        onegroup.m_list_zhipingInfo.Add(mi);
                        onegroup.m_list_zhipingInfo.Add(mi);
                    }
                    else
                    {
                        if (GlobalVar.gl_str_Product.Trim() == "") { return false; }
                        string sql = "";
                        if (!GlobalVar.gl_bMPNPlan)
                        {
                            sql = "SELECT  SubName,Value1,BarLen,StaPosition,[Valen] FROM BasCheckPart"
                                   + " where ClassName = 'MIC' AND ProductName = '" + GlobalVar.gl_str_Product + "' AND Invalid = '0' "
                                   + " and [FlowId] = '" + onegroup.FlowID + "' ";
                        }
                        else
                        {
                            //�ֽ�����ַ���//ƷĿ�ϲ�2017.07.14
                            List<string> list_assemble = new List<string>(GlobalVar.gl_strMpnAssemble.Split('-'));
                            string _assemble = list_assemble.Find(delegate (string ass)
                            {
                                return ass.Substring(3) == onegroup.FlowID.ToString("00");
                            });
                            sql = string.Format("SELECT distinct SubName,BarLen,Value1,StaPosition,Valen,Value2,StaPosition2,Valen2,FlowId FROM BasCheckPart" +
                                " WHERE productName='{0}' and fpccmtcode='{1}' and fpcposition={2} and Flowid ={3} and Invalid='0'",
                                GlobalVar.gl_str_Product, _assemble.Substring(2, 1), _assemble.Substring(0, 2), onegroup.FlowID);
                        }

                        DataTable dt1 = m_DBQuery.get_database_BaseData(sql);
                        if (dt1 != null)
                        {
                            for (int n = 0; n < dt1.Rows.Count; n++)
                            {
                                ZhiPinInfo mi = new ZhiPinInfo();
                                mi._SubName = dt1.Rows[0 + n]["SubName"].ToString();
                                mi._HeadStr = dt1.Rows[0 + n]["Value1"].ToString();
                                mi._BarcodeLength = Convert.ToInt32(dt1.Rows[0 + n]["BarLen"].ToString());
                                mi._StartPos = Convert.ToInt32(dt1.Rows[0 + n]["StaPosition"].ToString()) - 1;
                                mi._StartLen = Convert.ToInt32(dt1.Rows[0 + n]["Valen"].ToString());
                                onegroup.m_list_zhipingInfo.Add(mi);
                            }
                        }
                        if (onegroup.m_list_zhipingInfo.Count == 0)
                        {
                            MessageBox.Show("Mic������Ϣȱʧ!", "�e�`", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return false;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("��ѯMic������Ϣ����!", "�e�`", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            return true;
        }

        private bool getProxInfobyLotNo()
        {
            try
            {
                for (int i = 0; i < GlobalVar.gl_List_PointInfo.m_List_PointInfo.Count; i++)
                {
                    OnePointGroup onegroup = GlobalVar.gl_List_PointInfo.m_List_PointInfo[i];
                    onegroup.m_list_zhipingInfo.Clear();
                    //GlobalVar.gl_list_ZhiPinInfo.Clear();
                    if ((GlobalVar.gl_str_LotNo == "99999999999")
                        || (GlobalVar.gl_str_LotNo == "99999999998"))
                    {
                        //FOR TEST
                        ZhiPinInfo mi = new ZhiPinInfo();
                        mi._SubName = "KNOWLES";
                        mi._HeadStr = "FN8";
                        mi._BarcodeLength = 16;
                        mi._StartPos = 0;
                        mi._StartLen = 3;
                        onegroup.m_list_zhipingInfo.Add(mi);
                        return true;
                    }
                    if (GlobalVar.gl_str_Product.Trim() == "") { return false; }
                    //����ƷĿ�Ų�ѯPROX��Ʒ����ǰ3λ��������飬���û�м�¼�򱨴�---PROXһ��ƷĿֻ��һ����¼
                    string sql = "SELECT  TOP  1  [SubName],[Value1],[BarLen],[StaPosition],[Valen] FROM [BASEDATA].[dbo].[BasCheckPart] "
                             + " where ClassName = 'PROX' AND ProductName = '" + GlobalVar.gl_str_Product + "' AND Invalid = '0' ";
                    DataTable dt1 = m_DBQuery.get_database_BARDATA(sql);
                    if ((dt1 != null) && (dt1.Rows.Count > 0))
                    {
                        ZhiPinInfo mi = new ZhiPinInfo();
                        mi._HeadStr = dt1.Rows[0]["Value1"].ToString();
                        mi._SubName = dt1.Rows[0]["SubName"].ToString();
                        mi._BarcodeLength = Convert.ToInt32(dt1.Rows[0]["BarLen"].ToString());
                        mi._StartPos = Convert.ToInt32(dt1.Rows[0]["StaPosition"].ToString()) - 1;
                        mi._StartLen = Convert.ToInt32(dt1.Rows[0]["Valen"].ToString());
                        onegroup.m_list_zhipingInfo.Add(mi);
                    }
                    else
                    {
                        MessageBox.Show("�]�Ќ���ƷĿ��PROX�l�a��Ϣ", "�e�`", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                        return false;
                    }
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("ƷĿ��PROX�l�a��Ϣ��ԃ����:" + e.ToString());
                return false;
            }
            return true;
        }
        private bool getPcsInfobyLotNo()
        {
            try
            {
                for (int i = 0; i < GlobalVar.gl_List_PointInfo.m_List_PointInfo.Count; i++)
                {
                    OnePointGroup onegroup = GlobalVar.gl_List_PointInfo.m_List_PointInfo[i];
                    onegroup.m_list_zhipingInfo.Clear();
                    //GlobalVar.gl_list_ZhiPinInfo.Clear();
                    if ((GlobalVar.gl_str_LotNo == "99999999999")
                        || (GlobalVar.gl_str_LotNo == "99999999998"))
                    {
                        //FOR TEST
                        ZhiPinInfo mi = new ZhiPinInfo();
                        mi._SubName = "HC9X";
                        mi._HeadStr = "CKX";
                        mi._BarcodeLength = 22;
                        //mi._StartPos = 0;
                        //mi._StartLen = 3;
                        onegroup.m_list_zhipingInfo.Add(mi);
                        return true;
                    }
                    if (GlobalVar.gl_str_Product.Trim() == "") { return false; }
                    //����ƷĿ�Ų�ѯPCS��Ʒ����ǰ3λ��������飬���û�м�¼�򱨴�---PCSһ��ƷĿֻ��һ����¼
                    string sql = "SELECT DISTINCT [PPP],[EEEE],[BarLen] FROM [BASEDATA].[dbo].[ProjectBasic] WHERE [Product]='" + GlobalVar.gl_str_Product + "'";
                    DataTable dt1 = m_DBQuery.get_database_BARDATA(sql);
                    if ((dt1 != null) && (dt1.Rows.Count > 0))
                    {
                        ZhiPinInfo bi = new ZhiPinInfo();
                        bi._HeadStr = dt1.Rows[0]["PPP"].ToString();
                        bi._SubName = dt1.Rows[0]["EEEE"].ToString();
                        bi._BarcodeLength = Convert.ToInt32(dt1.Rows[0]["BarLen"].ToString());
                        //                         mi._StartPos = Convert.ToInt32(dt1.Rows[0]["StaPosition"].ToString()) - 1;
                        //                         mi._StartLen = Convert.ToInt32(dt1.Rows[0]["Valen"].ToString());
                        onegroup.m_list_zhipingInfo.Add(bi);

                    }
                    else
                    {
                        MessageBox.Show("�]�Ќ���ƷĿ��PCS�l�a��Ϣ", "�e�`", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                        return false;
                    }
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("ƷĿ��PCS�l�a��Ϣ��ԃ����:" + e.ToString());
                return false;
            }
            return true;
        }

        //���sheet����Ϸ���
        private bool CheckShtBarcode(String SheetBarcode)
        {
            try
            {
                //if (GlobalVar.gl_usermode == 1) return true;// ����ģʽ���ж�sheet���� [10/20/2017 617004]
                if (!myfunc.checkStringIsLegal(SheetBarcode, 3))
                {
                    SetSheetBarLabel("�l�a����Ҏ�t");
                    GlobalVar.gl_strShtBarcode = "";
                    return false; //�l�a����Ҏ�t
                }
                //if (SheetBarcode.Trim().Length != GlobalVar.gl_BarcodeLength_Sheet) //fortest sht
                if (SheetBarcode.Trim().Length <= 0) //fortest sht
                {
                    SetSheetBarLabel("Sheet�l�a�L�� <= 0");
                    GlobalVar.gl_strShtBarcode = "";
                    return false;
                }
                GlobalVar.gl_DBquery = new DBQuery(GlobalVar.gl_str_Product, "");//����Pcs������ݿ⹹�췽��
                int value = GlobalVar.gl_DBquery.CheckSheeBarCode(SheetBarcode);
                GlobalVar.gl_lsChkItem = DBQuery.GetErrorList();
                //if (value != 0) return false;
                int nIndex = GlobalVar.gl_lsChkItem.FindIndex(0, GlobalVar.gl_lsChkItem.Count, delegate (CheckItem chk) { return chk.ID == value; });
                if (nIndex >= 0)
                {
                    GlobalVar.gl_lsChkItem[nIndex].Count++;
                    if (value == 0)
                    {
                        SetSheetBarLabel(SheetBarcode.Trim());
                        GlobalVar.gl_strShtBarcode = SheetBarcode.Trim();
                    }
                    else
                    {
                        SetSheetBarLabel(SheetBarcode.Trim());
                        GlobalVar.gl_strShtBarcode = "";
                        ShowPsdErrForm sp = new ShowPsdErrForm("SHEET�l�a" + GlobalVar.gl_lsChkItem[nIndex].Name + ",Ո�������I!", false);
                        sp.ShowDialog();
                        return false;
                    }
                }
                return true;
            }
            catch { return false; }
            finally
            {
                this.Invoke(new Action(() => { button_sheetSNInfo.Text = GlobalVar.gl_strShtBarcode; }));
            }
        }

        private bool CheckShtBarcode_IC(String SheetBarcode)
        {
            try
            {
                //if (GlobalVar.gl_usermode == 1) return true;// ����ģʽ���ж�sheet���� [10/20/2017 617004]
                if (!myfunc.checkStringIsLegal(SheetBarcode, 3))
                {
                    SetSheetBarLabel("�l�a����Ҏ�t");
                    GlobalVar.gl_strShtBarcode = "";
                    return false; //�l�a����Ҏ�t
                }
                //if (SheetBarcode.Trim().Length != GlobalVar.gl_BarcodeLength_Sheet) //fortest sht
                if (SheetBarcode.Trim().Length <= 0) //fortest sht
                {
                    SetSheetBarLabel("Sheet�l�a�L�� <= 0");
                    GlobalVar.gl_strShtBarcode = "";
                    return false;
                }
                GlobalVar.gl_DBquery = new DBQuery(GlobalVar.gl_str_Product, "");//����Pcs������ݿ⹹�췽��
                int value = GlobalVar.gl_DBquery.CheckShtBarcode_IC(SheetBarcode);
                if (value != 0) return false;
                return true;
            }
            catch { return false; }
            finally
            {
                this.Invoke(new Action(() => { button_sheetSNInfo.Text = GlobalVar.gl_strShtBarcode; }));
            }
        }
        private void SetSheetBarLabel(string str)
        {
            try
            {
                this.Invoke(new Action(() =>
                {
                    button_sheetSNInfo.Text = str;
                }));
            }
            catch { }
        }
        #endregion

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {

        }

        private void ToolStripMenuItem_adminLogin_Click(object sender, EventArgs e)
        {
            if (GlobalVar.gl_usermode == 0)
            {
                LogonOn logon = new LogonOn();
                if (logon.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                { return; }
            }
            if (GlobalVar.gl_usermode == 0)
            {
                GlobalVar.gl_usermode = 1;
                ToolStripMenuItem_adminLogin.Text = "����ǳ�";
                toolStripLabel_opreatorType.Text = "��ǰģʽ��[����Ա]";
                baslerCCD1.StopCCD();
                AdminEnable(true);

            }
            else
            {
                GlobalVar.gl_usermode = 0;
                ToolStripMenuItem_adminLogin.Text = "�������";
                toolStripLabel_opreatorType.Text = "��ǰģʽ��[OP]";
                AdminEnable(false);
            }
        }
        private void AdminEnable(bool enable)
        {
            groupBox_refpointSetting.Enabled = groupBox_markPointSet.Enabled = enable;
            m_obj_dwg.setRefSettingEnable(enable);
            m_form_movecontrol.Enabled = enable;
            ToolStripMenuItem_exit.Enabled = enable;
            baslerCCD1.Enabled = enable;
            ToolStripMenuItem_RunTest.Enabled = enable;
            m_form_movecontrol.SetControlEnable(enable);
            btn_safetyDoorDispose.Enabled = enable;
            btn_safetyDoorEnable.Enabled = enable;
            panel_LodeType.Enabled = enable;
            txtbox_DeviceID.Enabled = enable;
            groupBox_LightControl.Enabled = enable;
            button1.Enabled = enable;
            if (enable)
            {
                TipPointShow.Width = 162;
            }
            else
                TipPointShow.Width = 0;
        }

        private void ToolStripMenuItem_reboot_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("�Ƿ�_���P�]��X�������ӣ�", "��ʾ", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning
                , MessageBoxDefaultButton.Button2) == DialogResult.OK)
            {
                ShutdownPC();
                Application.Exit();
            }
        }

        public static void ShutdownPC()
        {
            ProcessStartInfo PS = new ProcessStartInfo();
            PS.FileName = "shutdown.exe";
            PS.Arguments = "-r -t 1";
            Process.Start(PS);
        }

        private void ToolStripMenuItem_deleteHisPicture_Click(object sender, EventArgs e)
        {
            MessageBox.Show("һ��Ҫ���豸���е�ʱ��ִ�д˲���������ᵼ�³�����.���ȷ��������.", "����", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            if (m_inScanFunction) { return; }
            deleteOldPictures();
        }

        private void deleteOldPictures()
        {
            try
            {
                if (GlobalVar.gl_PicsSavePath.Trim() == "") { return; }
                DirectoryInfo DI = new DirectoryInfo(GlobalVar.gl_PicsSavePath);
                foreach (DirectoryInfo sub_DI in DI.GetDirectories())
                {
                    TimeSpan ts = DateTime.Now - sub_DI.CreationTime;
                    if (ts.TotalHours >= 48)
                    {
                        Directory.Delete(sub_DI.FullName, true);
                    }
                }
                if (GlobalVar.gl_NGPicsSavePath.Trim() == "") { return; }
                DirectoryInfo DII = new DirectoryInfo(GlobalVar.gl_NGPicsSavePath);
                foreach (DirectoryInfo sub_DI in DII.GetDirectories())
                {
                    TimeSpan ts = DateTime.Now - sub_DI.CreationTime;
                    if (ts.TotalHours >= 48)
                    {
                        Directory.Delete(sub_DI.FullName, true);
                    }
                }
                foreach (FileInfo sub_DI in DII.GetFiles())
                {
                    TimeSpan ts = DateTime.Now - sub_DI.CreationTime;
                    if (ts.TotalHours >= 48)
                    {
                        Directory.Delete(sub_DI.FullName, true);
                    }
                }
                MessageBox.Show("�ļ��h���ꮅ��");
            }
            catch
            {
                MessageBox.Show("�ļ��h��ʧ����Ո�M���քӄh����");
            }
        }

        private void ToolStripMenuItem_RunTest_Click(object sender, EventArgs e)
        {
            ThreadPool.QueueUserWorkItem(runCommand_withoutCapture);
        }

        private void button_alarm_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("�Ƿ�ȷ���Ӵ������쳣��", "��ʾ", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
                == System.Windows.Forms.DialogResult.Yes)
            {
                m_form_movecontrol.ResetAlarmError();
                m_form_movecontrol_eve_EmergenceRelease(null, null);
            }
        }

        #region оƬ�����Զ�ʶ��/��Դ��������
        private void toolStripButton_autoDetectMicType_Click(object sender, EventArgs e)
        {
            //if (GlobalVar.gl_List_PointInfo.Count < 5)
            //{
            //    MessageBox.Show("û�е���ƷĿ��Ϣ����Ʒ����������������LOTNO!", "�e�`", MessageBoxButtons.OK, MessageBoxIcon.Error);
            //    return; 
            //}
        }

        /// <param name="MICType">AAC/ST/GEORTEC/KNOWLES</param>
        public void SetLedLightAndExposure(string MICType)
        {
            //����MARK�㣬ȫ��
            if (MICType == "MARK")
            {
                GlobalVar.gl_paras_basler_Exposure_Calibrate = GlobalVar.gl_exposure_Mark_default;
                GlobalVar.gl_paras_basler_Exposure_Scan = GlobalVar.gl_exposure_Matrix_default;
                //���ON  �׹�ON
                m_form_movecontrol.SetDO(m_form_movecontrol.m_AxisNum_Z, 6, 1);
                m_form_movecontrol.SetDO(m_form_movecontrol.m_AxisNum_U, 6, 1);
                return;
            }
            this.Invoke(new Action(() =>
            {
                toolStripButton_autoDetectMicType.Text = "��ǰ��ҵ����:[" + MICType + "]";
            }));
            myfunc.ReadProductTypeExposure(MICType);
            if (GlobalVar.gl_LinkType == LinkType.PROX)
            {
                GlobalVar.gl_paras_basler_Exposure_Calibrate = GlobalVar.gl_exposure_Mark_default;
                GlobalVar.gl_paras_basler_Exposure_Scan = GlobalVar.gl_exposure_Matrix_default;
                //���ON  �׹�OFF
                m_form_movecontrol.SetDO(m_form_movecontrol.m_AxisNum_Z, 6, 1);
                m_form_movecontrol.SetDO(m_form_movecontrol.m_AxisNum_U, 6, 0);
            }
            else if (GlobalVar.gl_LinkType == LinkType.MIC)
            {
                #region //��Դ�ع�ֵ
                //switch (MICType.Trim().ToUpper())
                //{
                //    case "AAC":
                //        GlobalVar.gl_paras_basler_Exposure_Calibrate = GlobalVar.gl_exposure_Mark_AAC;
                //        GlobalVar.gl_paras_basler_Exposure_Scan = GlobalVar.gl_exposure_Matrix_AAC;
                //        //���ON  �׹�OFF
                //        m_form_movecontrol.SetDO(m_form_movecontrol.m_AxisNum_Z, 6, 1);
                //        m_form_movecontrol.SetDO(m_form_movecontrol.m_AxisNum_U, 6, 0);
                //        toolStripButton_autoDetectMicType.Text = "��Ʒ����:[AAC]";
                //        break;
                //    case "ST":
                //        GlobalVar.gl_paras_basler_Exposure_Calibrate = GlobalVar.gl_exposure_Mark_ST;
                //        GlobalVar.gl_paras_basler_Exposure_Scan = GlobalVar.gl_exposure_Matrix_ST;
                //        //���OFF �׹�ON 
                //        m_form_movecontrol.SetDO(m_form_movecontrol.m_AxisNum_Z, 6, 0);
                //        m_form_movecontrol.SetDO(m_form_movecontrol.m_AxisNum_U, 6, 1);
                //        break;
                //    case "GEORTEK":
                //    case "GOERTEK":
                //        GlobalVar.gl_paras_basler_Exposure_Calibrate = GlobalVar.gl_exposure_Mark_Geortek;
                //        GlobalVar.gl_paras_basler_Exposure_Scan = GlobalVar.gl_exposure_Matrix_Geortek;
                //        //���ON  �׹�OFF 
                //        m_form_movecontrol.SetDO(m_form_movecontrol.m_AxisNum_Z, 6, 1);
                //        m_form_movecontrol.SetDO(m_form_movecontrol.m_AxisNum_U, 6, 0);
                //        toolStripButton_autoDetectMicType.Text = "��Ʒ����:[GOERTEK]";
                //        break;
                //    case "KNOWLES": //δ��֤
                //        GlobalVar.gl_paras_basler_Exposure_Calibrate = GlobalVar.gl_exposure_Mark_Knowles;
                //        GlobalVar.gl_paras_basler_Exposure_Scan = GlobalVar.gl_exposure_Matrix_Knowles;
                //        //���ON  �׹�OFF
                //        m_form_movecontrol.SetDO(m_form_movecontrol.m_AxisNum_Z, 6, 1);
                //        m_form_movecontrol.SetDO(m_form_movecontrol.m_AxisNum_U, 6, 1);
                //        toolStripButton_autoDetectMicType.Text = "��Ʒ����:[KNOWLES]";
                //        break;
                //    default:
                //        GlobalVar.gl_paras_basler_Exposure_Calibrate = GlobalVar.gl_exposure_Mark_default;
                //        GlobalVar.gl_paras_basler_Exposure_Scan = GlobalVar.gl_exposure_Matrix_default;
                //        //���ON  �׹�OFF
                //        m_form_movecontrol.SetDO(m_form_movecontrol.m_AxisNum_Z, 6, 1);
                //        m_form_movecontrol.SetDO(m_form_movecontrol.m_AxisNum_U, 6, 0);
                //        toolStripButton_autoDetectMicType.Text = "��Ʒ����:[KNOWLES]";
                //        break;
                //}
                #endregion
                #region ��Դ�ع�ֵNEW
                GlobalVar.gl_paras_basler_Exposure_Scan = GlobalVar.gl_Model_exposure;
                //���ON  �׹�OFF
                m_form_movecontrol.SetDO(m_form_movecontrol.m_AxisNum_Z, 6, byte.Parse(GlobalVar.gl_Model_redLight.ToString()));
                m_form_movecontrol.SetDO(m_form_movecontrol.m_AxisNum_U, 6, byte.Parse(GlobalVar.gl_Model_whiteLight.ToString()));
                #endregion
            }
            else if (GlobalVar.gl_LinkType == LinkType.BARCODE)
            {
                GlobalVar.gl_paras_basler_Exposure_Calibrate = GlobalVar.gl_exposure_Mark_default;
                GlobalVar.gl_paras_basler_Exposure_Scan = GlobalVar.gl_exposure_Matrix_default;
                //���ON  �׹�ON
                m_form_movecontrol.SetDO(m_form_movecontrol.m_AxisNum_Z, 6, 1);
                m_form_movecontrol.SetDO(m_form_movecontrol.m_AxisNum_U, 6, 1);
            }
            else if (GlobalVar.gl_LinkType == LinkType.IC)
            {
                GlobalVar.gl_paras_basler_Exposure_Calibrate = GlobalVar.gl_exposure_Mark_default;
                GlobalVar.gl_paras_basler_Exposure_Scan = GlobalVar.gl_exposure_Matrix_default;
                //���ON  �׹�ON
                m_form_movecontrol.SetDO(m_form_movecontrol.m_AxisNum_Z, 6, 1);
                m_form_movecontrol.SetDO(m_form_movecontrol.m_AxisNum_U, 6, 1);
            }
        }
        #endregion

        #region ����ļ��ϴ�
        Thread m_thread_Upload;
        bool m_inUploading = false;  //�Ƿ������ϴ���
        private string m_iniFullName;
        private string m_str_Connstr = "";     //���ݿ������ִ�--��sheet.ini�л�ȡ
        private List<pcsinfo> m_list_info = new List<pcsinfo>();
        private List<string> m_listNGPosition_preScan = new List<string>();
        private string m_SheetBarcode; //�ϴ�sheetbarcode
        public uint m_AxisNum_X = 0;
        public uint m_AxisNum_Y = 1;
        public uint m_AxisNum_Z = 2;
        public uint m_AxisNum_U = 3;
        IntPtr[] m_Axishand = new IntPtr[32];
        private bool m_tag_boardArrived = false;
        private void startUpload()
        {
            logWR.appendNewLogMessage("����Ƿ�����Ҫ�ϴ����ļ�");
            if (!m_inUploading)
            {
                if (!Directory.Exists(GlobalVar.gl_path_FileBackUp))
                {
                    MessageBox.Show("�������ϴ���������ļ��У�" + GlobalVar.gl_path_FileBackUp + "�����ڲ����������趨��");
                    return;
                }
                if (!Directory.Exists(GlobalVar.gl_Directory_savePath))
                {
                    MessageBox.Show("�������ϴ�����ļ��У�" + GlobalVar.gl_Directory_savePath + "����ȷ�ϡ�");
                    return;
                }
                if (m_thread_Upload == null)
                {
                    m_thread_Upload = new Thread(selectFile);
                }
                if (!m_thread_Upload.IsAlive)
                {
                    if (m_thread_Upload.ThreadState == System.Threading.ThreadState.Aborted)
                    {
                        m_thread_Upload = new Thread(selectFile);
                    }
                    m_thread_Upload.Priority = ThreadPriority.AboveNormal;
                    m_thread_Upload.IsBackground = true;
                    m_thread_Upload.Start();
                }
                m_inUploading = true;
                button1.BackColor = Color.Green;
                button1.Text = "�����ϴ�";
            }
        }

        private void button_startUpload_Click(object sender, EventArgs e)
        {
            try
            {
                if (!m_inUploading)
                {
                    if (!Directory.Exists(GlobalVar.gl_path_FileBackUp))
                    {
                        MessageBox.Show("�������ϴ���������ļ��У�" + GlobalVar.gl_path_FileBackUp + "�����ڲ����������趨��");
                        return;
                    }
                    if (!Directory.Exists(GlobalVar.gl_Directory_savePath))
                    {
                        MessageBox.Show("�������ϴ�����ļ��У�" + GlobalVar.gl_Directory_savePath + "����ȷ�ϡ�");
                        return;
                    }
                    if (m_thread_Upload == null)
                    {
                        m_thread_Upload = new Thread(selectFile);
                    }
                    if (!m_thread_Upload.IsAlive)
                    {
                        if (m_thread_Upload.ThreadState == System.Threading.ThreadState.Aborted)
                        {
                            m_thread_Upload = new Thread(selectFile);
                        }
                        m_thread_Upload.Priority = ThreadPriority.AboveNormal;
                        m_thread_Upload.IsBackground = true;
                        m_thread_Upload.Start();
                    }
                    m_inUploading = true;
                    button1.BackColor = Color.Green;
                    button1.Text = "�����ϴ�";
                }
                else
                {
                    if ((m_thread_Upload != null) && m_thread_Upload.IsAlive)
                    { m_thread_Upload.Abort(); }
                    button1.BackColor = SystemColors.Control;
                    m_inUploading = false;
                    button1.Text = "��ʼ�ϴ�";
                }
            }
            catch { }
        }
        private void selectFile(object obj)
        {
            for (; ; )
            {
                try
                {
                    Thread.Sleep(500);
                    FileInfo[] FileinfoList = new DirectoryInfo(GlobalVar.gl_Directory_savePath).GetFiles("*.ini");
                    if (FileinfoList.Length == 0)
                    {
                        showStatus("�]����Ҫ�ϴ��Ĕ����ļ�");
                        continue;
                    }
                    else
                    {
                        for(int j = 0; j< FileinfoList.Length; j++)
                        {
                            m_SheetBarcode = "";
                            m_list_info.Clear();
                            m_iniFullName = FileinfoList[j].FullName;
                            showStatus("�����ļ�" + m_iniFullName + "������......");
                            logWR.appendNewLogMessage("�����ļ�" + m_iniFullName + "������......");
                            string filename = Path.GetFileNameWithoutExtension(m_iniFullName);
                            m_SheetBarcode = filename.Substring(0, filename.IndexOf("_"));
                            if (m_SheetBarcode == "")
                            {
                                logWR.appendNewLogMessage("Sheet����Ϊ�գ�");
                                try { File.Delete(m_iniFullName); }
                                catch { }
                                continue; 
                            }
                            StringBuilder str_tmp = new StringBuilder(500);
                            //���ݿ������ִ�
                            GetPrivateProfileString(GlobalVar.gl_iniSection_Result, GlobalVar.gl_iniKey_ConnStr, "", str_tmp, 500, m_iniFullName);
                            m_str_Connstr = str_tmp.ToString().Trim();
                            m_str_Connstr += "uid=suzmektec;pwd=suzmek;Connect Timeout=5";
                            if (m_str_Connstr.Trim() == "")
                            {
                                logWR.appendNewLogMessage("���ݿ������ַ���Ϊ��!");
                                continue;
                            }
                            //��ȡflowid
                            int _flowid = 0; //����������ݿ��м�¼�ľͻ���0
                            GetPrivateProfileString(GlobalVar.gl_iniSection_Result, GlobalVar.gl_iniKey_FlowID, "", str_tmp, 500, m_iniFullName);

                            if (str_tmp.ToString().Trim().Length > 0)
                            {
                                _flowid = Convert.ToInt32(str_tmp.ToString().Trim());
                            }
                            FileStream FS = new FileStream(m_iniFullName, FileMode.Open);
                            StreamReader sr = new StreamReader(FS);
                            string totalstr = sr.ReadToEnd();
                            string[] resultList = totalstr.Split('\r');
                            for (int i = 1; i <= resultList.Length - 1; i++)
                            {
                                string result = resultList[i].Trim('\r').Trim('\n').Trim();
                                if (result.IndexOf("=") < 0) { continue; }  //����Ҫ��=
                                string posstr = result.Substring(0, result.IndexOf("="));  //λ�ú�
                                string Barcode = result.Substring(result.IndexOf("=") + 1);

                                if (posstr.Length == 0) { continue; } //0
                                if (posstr.Length > 3) { continue; } //���ᳬ��1000
                                if (!myfunc.checkStringIsLegal(posstr, 1)) { continue; }  //�����������ַ�

                                pcsinfo pi = new pcsinfo();
                                pi._flowID = _flowid;
                                pi._posNum = posstr;
                                GetPrivateProfileString(GlobalVar.gl_iniSection_Result, posstr, "", str_tmp, 50, m_iniFullName);
                                pi._MicBarcode = str_tmp.ToString().Trim();
                                m_list_info.Add(pi);
                            }
                            FS.Close();
                            StartDBQueryAndSave();
                            logWR.appendNewLogMessage("�����ļ�" + m_iniFullName + "�ϴ��ꮅ���ȴ��������");
                            string path = GlobalVar.gl_path_FileBackUp + "\\" + DateTime.Now.ToString("yyyyMMdd") + "\\";
                            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                            File.Copy(m_iniFullName, path + Path.GetFileName(m_iniFullName), true);
                            logWR.appendNewLogMessage("���ݱ�����ɣ��ȴ�ɾ���ļ����");
                            File.Delete(m_iniFullName);
                            logWR.appendNewLogMessage("�����ļ�" + m_iniFullName + "��ɾ����");
                            showStatus("�����ļ�" + m_iniFullName + "�����ꮅ��");
                        }
                    }
                    //Thread.Sleep(500);  //�ȴ����ݴ洢���
                }
                catch (Exception ex)
                {
                    logWR.appendNewLogMessage("�ϴ��ļ������쳣:" +ex.ToString());
                    updateLedLightStatus(2);
                    ShowPsdErrForm form = new ShowPsdErrForm("�ϴ��ļ������쳣!", true);
                    form.ShowDialog();
                    updateLedLightStatus(0);
                }
            }
        }

        private void showStatus(string str)
        {
            try
            {
                this.BeginInvoke(new Action(() =>
                {
                    toolStripStatusLabel_uploadInfo.Text = str;
                }));
            }catch(Exception ex)
            {
                logWR.appendNewLogMessage("ˢ�½��� �쳣:"+ex.ToString());
            }
        }

        private void StartDBQueryAndSave()
        {
            logWR.appendNewLogMessage("��ʼ�ϴ�����");
            DBQuery query = new DBQuery(m_str_Connstr);

            for (int i = 0; i < m_list_info.Count; i++)
            {
                if (m_list_info[i] != null)
                {
                    int result;
                    if (m_list_info[i]._flowID == 5 && m_list_info[i]._MicBarcode != "")
                    {
                        query = new DBQuery(GlobalVar.gl_str_Product, GlobalVar.gl_str_LotNo);
                        GlobalVar.gl_strShtBarcode = m_SheetBarcode;
                        result = query.FPCBarcodeLink(m_list_info[i]._MicBarcode, Convert.ToInt32(m_list_info[i]._posNum));
                    }
                    else if (m_list_info[i]._MicBarcode != "")
                    {
                        switch (GlobalVar.gl_ProductModel)
                        {
                            case "A41SENSOR":
                                if (GlobalVar.gl_LinkType == LinkType.PROX)
                                    result = query.PROXBarcodeLinkSensor(m_SheetBarcode, m_list_info[i]._MicBarcode, m_list_info[i]._posNum);
                                else if (GlobalVar.gl_LinkType == LinkType.MIC)
                                    result = query.MICBarcodeLinkSensor(m_SheetBarcode, m_list_info[i]._MicBarcode, m_list_info[i]._posNum);
                                break;
                            case "A42SENSOR":
                                if (GlobalVar.gl_LinkType == LinkType.PROX)
                                    result = query.PROXBarcodeLinkSensor(m_SheetBarcode, m_list_info[i]._MicBarcode, m_list_info[i]._posNum);
                                else if (GlobalVar.gl_LinkType == LinkType.MIC)
                                    result = query.MICBarcodeLinkSensor(m_SheetBarcode, m_list_info[i]._MicBarcode, m_list_info[i]._posNum);
                                break;
                            default:
                                result = query.MICBarcodeLink(m_SheetBarcode, m_list_info[i]._MicBarcode, m_list_info[i]._posNum, m_list_info[i]._flowID);
                                if (result == 7)
                                {
                                    ShowPsdErrForm err = new ShowPsdErrForm("�Ѵ��ڹ�����¼,��ֹʹ��!", false);
                                    err.ShowDialog();
                                    return;
                                }
                                break;
                        }
                    }
                }
            }
            logWR.appendNewLogMessage("�ϴ��������");
        }

        class pcsinfo
        {
            public bool _isValidPos;
            public string _posNum;
            public string _MicBarcode;
            public string _ProductName; //����
            public int _flowID; //MIC��Prox��FlowID
        }
        #endregion

        private void toolStripButtonStop_Click(object sender, EventArgs e)
        {

        }

        public void setRefPointValue(string x, string y)
        {
            textBox_fixPoint_x.Text = x;
            textBox_fixPoint_y.Text = y;
            textBox_fixPoint_z.Text = "0.0";
        }
        public void SetCalPosValue(SPoint sp)
        {
            try
            {
                this.Invoke(new Action(() =>
                {
                    textBox_CalPos_X.Text = sp.Pos_X.ToString("000.000");
                    textBox_CalPos_Y.Text = sp.Pos_Y.ToString("000.000");
                }));
                myfunc.WriteCalPositionInfoToTBS();
            }
            catch { }
        }

        private void button_CalPosMove_Click(object sender, EventArgs e)
        {
            float Pos_X = float.Parse(textBox_CalPos_X.Text);
            float Pos_Y = float.Parse(textBox_CalPos_Y.Text);
            GlobalVar.gl_point_CalPos.Pos_X = Pos_X;
            GlobalVar.gl_point_CalPos.Pos_Y = Pos_Y;
            //m_obj_dwg_eve_sendCalPosition(GlobalVar.gl_Ref_Point_CADPos.Pos_X - Pos_X, GlobalVar.gl_Ref_Point_CADPos.Pos_Y - Pos_Y);
            myfunc.WriteCalPositionInfoToTBS();
            Thread thd = new Thread(new ThreadStart(delegate
            {
                if (m_form_movecontrol.CheckAxisInMoving())
                {
                    MessageBox.Show("�豸(��)ΪNot Ready״̬����ֹͣ���������I��", "��ʾ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                float dis_X = (GlobalVar.gl_Ref_Point_CADPos.Pos_X - (Pos_X)) * -1; //��еԭ��������,X������Ҫȡ��
                float dis_Y = GlobalVar.gl_Ref_Point_CADPos.Pos_Y - (Pos_Y);
                float x = dis_X + GlobalVar.gl_value_CalibrateDis_X + dis_Y * GlobalVar.gl_value_CalibrateRatio_X;
                float y = dis_Y + GlobalVar.gl_value_CalibrateDis_Y + dis_X * GlobalVar.gl_value_CalibrateRatio_Y;
                m_form_movecontrol.FixPointMotion(x, y, 3);
            }));
            thd.IsBackground = true;
            thd.Start();
        }

        private void btn_safetyDoorDispose_Click(object sender, EventArgs e)
        {
            m_form_movecontrol.SetDO(m_form_movecontrol.m_AxisNum_U, 7, 0); //ǰ���ͷ�
        }

        private void btn_safetyDoorEnable_Click(object sender, EventArgs e)
        {
            m_form_movecontrol.SetDO(m_form_movecontrol.m_AxisNum_U, 7, 1); //ǰ������
        }

        private void button_RtnRefOrgPoint_Click(object sender, EventArgs e)
        {
            //m_obj_dwg.button_RtnRefOrgPoint_Click(sender, e);    //�ص��ο�ԭ��

            try
            {
                SPoint sp = new SPoint();
                //sp.Pos_X = float.Parse(comboBox_fixPoint_x.Text + textBox_fixPoint_x.Text);
                //sp.Pos_Y = float.Parse(comboBox_fixPoint_y.Text + textBox_fixPoint_y.Text);
                //sp.Pos_Z = float.Parse(comboBox_fixPoint_z.Text + textBox_fixPoint_z.Text);
                sp.Pos_X = float.Parse(textBox_fixPoint_x.Text);
                sp.Pos_Y = float.Parse(textBox_fixPoint_y.Text);
                sp.Pos_Z = float.Parse(textBox_fixPoint_z.Text);
                m_obj_dwg_eve_sendReFPoint(sp);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }



        private void txtbox_DeviceID_TextChanged(object sender, EventArgs e)
        {
            if (txtbox_DeviceID.Text == "")
                return;
            else
                GlobalVar.gl_DeviceID = txtbox_DeviceID.Text.Trim();
        }

        private void rdb_LocalLoading_CheckedChanged(object sender, EventArgs e)
        {
            if (rdb_LocalLoading.Checked)
            {
                GlobalVar.gl_AutoLoadType = false;
            }
            else
            {
                GlobalVar.gl_AutoLoadType = true;
            }

        }

        private void button_RefOrgPoint_Click(object sender, EventArgs e)
        {
            returnreferecePoint();
        }



        private void button_CCDTrigger_Click(object sender, EventArgs e)
        {
            CaptureTrigger();
        }

        private void CaptureTrigger()
        {
            Thread.Sleep(60); //�ȴ���λ���ȶ�
            m_form_movecontrol.SetDO(m_AxisNum_X, 4, 1);
            Thread.Sleep(2);
            m_form_movecontrol.SetDO(m_AxisNum_X, 4, 0);
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            #region Z/U��DO
            byte byte_Z = 0;
            byte byte_U = 0;
            Motion.mAcm_AxDoGetBit(m_Axishand[m_AxisNum_Z], 6, ref byte_Z);
            pictureBox_ledRed.BackColor = (byte_Z == 0) ? Color.Gray : Color.Red;
            button_LedRed.Text = (byte_Z == 0) ? "ON" : "OFF";
            Motion.mAcm_AxDoGetBit(m_Axishand[m_AxisNum_U], 6, ref byte_U);
            pictureBox_ledWhite.BackColor = (byte_U == 0) ? Color.Gray : Color.Red;
            button_LedWhite.Text = (byte_U == 0) ? "ON" : "OFF";
            #endregion

            #region U/Z��DI
            byte BitIn_Z = 0;
            //Z��IN0
            UInt32 Result_ZU = Motion.mAcm_AxDiGetBit(m_Axishand[m_AxisNum_Z], 0, ref BitIn_Z);
            if (Result_ZU == (uint)ErrorCode.SUCCESS)
            {
                if (BitIn_Z == 1)
                {
                    //֪ͨ��������ɨ��λ��
                    pictureBox_ShtSNScan.BackColor = Color.Red;
                }
                else
                {
                    pictureBox_ShtSNScan.BackColor = Color.Gray;
                }
            }
            #endregion

            #region ��ȡLED I/O��Դ��Ϣ
            byte byte_Power = 0;
            uint Result_red = Motion.mAcm_AxDoGetBit(m_Axishand[m_AxisNum_Z], (ushort)2, ref byte_Power);
            if (Result_red == (uint)ErrorCode.SUCCESS)
            {
                if (byte_Power == 1)
                {
                    pictureBox_ledRed.BackColor = System.Drawing.Color.Red;
                }
                else
                {
                    pictureBox_ledRed.BackColor = System.Drawing.Color.Gray;
                }
            }
            uint Result_blue = Motion.mAcm_AxDoGetBit(m_Axishand[m_AxisNum_U], (ushort)2, ref byte_Power);
            if (Result_blue == (uint)ErrorCode.SUCCESS)
            {
                if (byte_Power == 1)
                {
                    pictureBox_ledWhite.BackColor = System.Drawing.Color.Red;
                }
                else
                {
                    pictureBox_ledWhite.BackColor = System.Drawing.Color.Gray;
                }
            }
            #endregion
        }

        private void button_setRefPoint_Click(object sender, EventArgs e)
        {

            try
            {
                SPoint sp = new SPoint();
                //sp.Pos_X = float.Parse(comboBox_fixPoint_x.Text + textBox_fixPoint_x.Text);
                //sp.Pos_Y = float.Parse(comboBox_fixPoint_y.Text + textBox_fixPoint_y.Text);
                //sp.Pos_Z = float.Parse(comboBox_fixPoint_z.Text + textBox_fixPoint_z.Text);
                sp.Pos_X = float.Parse(textBox_fixPoint_x.Text);
                sp.Pos_Y = float.Parse(textBox_fixPoint_y.Text);
                sp.Pos_Z = float.Parse(textBox_fixPoint_z.Text);
                m_obj_dwg_eve_sendReFPoint(sp);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void button_LedRedOff_Click(object sender, EventArgs e)
        {
            button_LedRedOff.Enabled = false;
            button_LedRed.BackColor = Color.SteelBlue;
            byte value = (button_LedRedOff.Text.ToUpper() == "ON") ? (byte)1 : (byte)0;
            button_LedRedOff.Text = "";
            uint result = m_form_movecontrol.SetDO(m_AxisNum_Z, 6, value);
            button_LedRed.Enabled = true;
            button_LedRedOff.BackColor = Color.Gray;
            button_LedRed.Text = "ON";
        }

        private void button_WhiteRedOff_Click(object sender, EventArgs e)
        {
            button_LedWhiteOff.Enabled = false;
            button_LedWhite.BackColor = Color.SteelBlue;
            byte value = (button_LedWhiteOff.Text.ToUpper() == "ON") ? (byte)1 : (byte)0;
            button_LedWhiteOff.Text = "";
            uint result = m_form_movecontrol.SetDO(m_AxisNum_U, 6, value);
            button_LedWhite.Enabled = true;
            button_LedWhiteOff.BackColor = Color.Gray;
            button_LedWhite.Text = "ON";
        }

        private void button_LedRed_Click(object sender, EventArgs e)
        {
            button_LedRedOff.Enabled = true;
            button_LedRedOff.BackColor = Color.SteelBlue;
            byte value = (button_LedRed.Text.ToUpper() == "ON") ? (byte)1 : (byte)0;
            button_LedRed.Text = "";
            uint result = m_form_movecontrol.SetDO(m_AxisNum_Z, 6, value);
            button_LedRed.Enabled = false;
            button_LedRed.BackColor = Color.Red;
            button_LedRedOff.Text = "OFF";
        }

        private void button_LedWhite_Click(object sender, EventArgs e)
        {
            button_LedWhiteOff.Enabled = true;
            button_LedWhiteOff.BackColor = Color.SteelBlue;
            byte value = (button_LedWhite.Text.ToUpper() == "ON") ? (byte)1 : (byte)0;
            button_LedWhite.Text = "";
            uint result = m_form_movecontrol.SetDO(m_AxisNum_U, 6, value);
            button_LedWhite.Enabled = false;
            button_LedWhite.BackColor = Color.Red;
            button_LedWhiteOff.Text = "OFF";
        }

        private void rdb_NetLoading_CheckedChanged(object sender, EventArgs e)
        {
            if (rdb_LocalLoading.Checked)
            {
                GlobalVar.gl_AutoLoadType = true;
            }
            else
            {
                GlobalVar.gl_AutoLoadType = false;
            }
        }

        private void richTextBox_SingleShow_TextChanged(object sender, EventArgs e)
        {
        }

        public void addLogStr(string Msg)
        {
            logWR.appendNewLogMessage(Msg);
            //richTextBox_SingleShow.AppendText(Msg);
        }


        private void autoDeleteOldPic()
        {
            try
            {
                if (checkHardDiskSpace("D") >= 0.8)
                {
                    if (GlobalVar.gl_PicsSavePath.Trim() == "") { return; }
                    DirectoryInfo DI = new DirectoryInfo(GlobalVar.gl_PicsSavePath);
                    foreach (DirectoryInfo sub_DI in DI.GetDirectories())
                    {
                        TimeSpan ts = DateTime.Now - sub_DI.CreationTime;
                        if (ts.TotalHours >= 48 && checkHardDiskSpace("D") >= 0.5)
                        {
                            Directory.Delete(sub_DI.FullName, true);
                        }
                    }
                    if (GlobalVar.gl_NGPicsSavePath.Trim() == "") { return; }
                    DirectoryInfo DII = new DirectoryInfo(GlobalVar.gl_NGPicsSavePath);
                    foreach (DirectoryInfo sub_DI in DII.GetDirectories())
                    {
                        TimeSpan ts = DateTime.Now - sub_DI.CreationTime;
                        if (ts.TotalHours >= 48 && checkHardDiskSpace("D") >= 0.5)
                        {
                            Directory.Delete(sub_DI.FullName, true);
                        }
                    }
                    foreach (FileInfo sub_DI in DII.GetFiles())
                    {
                        TimeSpan ts = DateTime.Now - sub_DI.CreationTime;
                        if (ts.TotalHours >= 48 && checkHardDiskSpace("D") >= 0.5)
                        {
                            Directory.Delete(sub_DI.FullName, true);
                        }
                    }
                    MessageBox.Show("�ļ��h���ꮅ��");
                }
            }
            catch
            {
                MessageBox.Show("�ļ��h��ʧ����Ո�M���քӄh����");
            }
        }

        /// <summary>
        /// ��ȡ�������ÿռ����
        /// </summary>
        /// <param name="str_HardDiskName"></param>
        /// <returns></returns>
        public static double checkHardDiskSpace(string str_HardDiskName)
        {
            double freeSpace = 0;
            double totalSize = 0;
            double usedSize = 0;
            double usedPercent = 0;
            str_HardDiskName = str_HardDiskName + ":\\";
            System.IO.DriveInfo[] drives = System.IO.DriveInfo.GetDrives();
            foreach (System.IO.DriveInfo drive in drives)
            {
                if (drive.Name == str_HardDiskName)
                {
                    freeSpace = drive.TotalFreeSpace / (1024 * 1024 * 1024);
                    totalSize = drive.TotalSize / (1024 * 1024 * 1024);
                    usedSize = totalSize - freeSpace;
                    usedPercent = usedSize / totalSize;
                }
            }
            return usedPercent;
        }

        private void toolStripLabel2_Click(object sender, EventArgs e)
        {
            GlobalVar.m_ScanAuthorized = true;
            test_startScanFunction();
        }

        private void test_startScanFunction()
        {
            try
            {
                timetest = DateTime.Now;
                OneCircleReset();
                //checkBlocksIsValid();  //--�ƶ����������ڽ��в�ѯ
                test_CalibrateAction();
            }
            catch (Exception e)
            {
                logWR.appendNewLogMessage("����ɨ������쳣 startScanFunction Error:  \r\n " + e.ToString());
            }

        }

        private void testToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string m_str = "Data Source=SUZSQLV01;database=A82TFLEX;uid=suzmektec;pwd=suzmek;Connect Timeout=5";
            DBQuery query = new DBQuery(m_str);
            int result = query.MICBarcodeLink("TESTsheet1", "TESTBarcode1", "1", 21);
        }
    }

    class ScanbarcodeInfo
    {
        public ScanbarcodeInfo() { }
        public string barcode = "";
        public List<int> NGPositionlist = new List<int>();
        public string LotNo = "";
        public bool LotResult = true;
        public string ErrMsg_Lot = "";
        public bool MICResult = true;
        public string ErrMsg_MIC = "";
        public void Clear()
        {
            try
            {
                barcode = "";
                NGPositionlist = new List<int>();
                LotNo = "";
                LotResult = true;
                ErrMsg_Lot = "";
                MICResult = true;
                ErrMsg_MIC = "";
            }
            catch { }
        }
        public void Clone(ScanbarcodeInfo info)
        {
            barcode = info.barcode;
            NGPositionlist.Clear();
            if (info.NGPositionlist.Count > 0)
            {
                NGPositionlist.AddRange(info.NGPositionlist);
            }
            LotNo = info.LotNo;
            LotResult = info.LotResult;
            ErrMsg_Lot = info.ErrMsg_Lot;
        }
    }

    #region CAD������Ϣ���
    public class SPoint : Object
    {
        public int FlowID;  //��DWG�л�ȡ
        public string Point_name;
        public int PointNumber; //ֵ���֞����µ����̖
        public double Angle_deflection = 0;   //���D�Ƕȣ����ּ��r����
        public int Line_sequence = 0;   //�������˳�� -- 0������ 1������
        public float Pos_X = 0.0F;
        public float Pos_Y = 0.0F;
        public float Pos_Z = 0.0F;

        public void CopyPoint(SPoint sp)
        {
            this.Pos_X = sp.Pos_X;
            this.Pos_Y = sp.Pos_Y;
            this.Pos_Z = sp.Pos_Z;
        }
    }

    public class ZhiPinInfo
    {
        public string _SubName;   //ST,KNOWLES...
        public string _HeadStr;      //FFC, FF9....
        public int _BarcodeLength;
        public int _StartPos;
        public int _StartLen;  //
    }

    //һ��OnePointGroup == һ��FlowID��cycle��DOCK������MIC��������CYCLE
    public class OnePointGroup
    {
        public int FlowID;   //FLOWID
        public List<SPoint> m_ListGroup = new List<SPoint>();
        public List<ZhiPinInfo> m_list_zhipingInfo = new List<ZhiPinInfo>();  //��ǰ���õ���Ʒ�������л���Դ
        public OneGroup_Blocks m_BlockList_ByGroup = new OneGroup_Blocks();  //��ǰ���BLOCK����        
    }

    //CAD���󼯺���Ϣ
    public class PointInfo
    {
        public List<OnePointGroup> m_List_PointInfo = new List<OnePointGroup>();
        public void addPoint(SPoint sp)
        {
            for (int i = 0; i < m_List_PointInfo.Count; i++)
            {
                if (m_List_PointInfo[i].FlowID == sp.FlowID)
                {
                    m_List_PointInfo[i].m_ListGroup.Add(sp); //һ��Flowid�ĵ����һ��,һ��Flowid��ֻ��һ��m_List_PointInfo
                    return;
                }
            }
            //���������û�б�return��˵��m_List_PointInfo����û�����FLOWID�ļ���
            OnePointGroup newgroup = new OnePointGroup();
            newgroup.FlowID = sp.FlowID;
            newgroup.m_ListGroup.Add(sp);
            m_List_PointInfo.Add(newgroup);
        }

        public void clearList()
        {
            m_List_PointInfo.Clear();
        }

        public void Sort()
        {
            for (int i = 0; i < m_List_PointInfo.Count; i++)
            {
                m_List_PointInfo[i].m_ListGroup.Sort(new SPointCompare_byTipSequence());
            }
        }
    }

    public class OneGroup_Blocks
    {
        public int FlowID;
        public List<DetailBlock> m_BlockinfoList = new List<DetailBlock>();
        public bool m_DecodeFinished = false;
        public void add(DetailBlock block)
        {
            m_BlockinfoList.Add(block);
        }

        //����������BLOCK�е�����
        public void CycleDecodeAllBlocks()
        {
            Thread threaddecode = new Thread(new ThreadStart(delegate
            {
                for (; ; )
                {
                    try
                    {
                        m_DecodeFinished = true;
                        for (int i = 0; i < m_BlockinfoList.Count; i++)
                        {
                            if (GlobalVar.gl_inEmergence) { break; }
                            if (!GlobalVar.gl_safetyDoor_Front && GlobalVar.gl_usermode == 0) break;
                            if ((!m_BlockinfoList[i].m_decodeFinished)
                                && (m_BlockinfoList[i].m_receivedPics))
                            {
                                m_BlockinfoList[i].backthread_decode_Halcon(FlowID);
                            }
                            m_DecodeFinished &= m_BlockinfoList[i].m_decodeFinished;
                        }
                        if (m_DecodeFinished) { break; }
                        if (GlobalVar.gl_inEmergence) { break; }
                        if (!GlobalVar.gl_safetyDoor_Front && GlobalVar.gl_usermode == 0) break;
                        Thread.Sleep(100);
                    }
                    catch (Exception e)
                    {
                        string str = "����������BLOCK�е�����(CycleDecodeAllBlocks)���̳���; \r\n" + e.ToString();
                        logWR.appendNewLogMessage(str);
                        MessageBox.Show(str);
                    }
                }
                m_DecodeFinished = true;  //��ʵ�Ƕ��ڵģ�ֻ��Ϊ�����̿���������
                clearALLHalcomMemory();
            }));
            threaddecode.IsBackground = true;
            threaddecode.Start();
        }

        private void clearALLHalcomMemory()
        {
            return;
            for (int i = 0; i < m_BlockinfoList.Count; i++)
            {
                try
                {
                    m_BlockinfoList[i].gl_halcon.releaseMemory();
                }
                catch (Exception e)
                {
                    string str = "����HALCON�ڴ�(clearALLHalcomMemory)���̳���; \r\n" + e.ToString();
                    logWR.appendNewLogMessage(str);
                    MessageBox.Show(str);
                }
            }
        }
    }
    #endregion

    #region BLOCK��ʾ���������
    public class BlockInfo
    {
        public float Pos_X;
        public float Pos_Y;
        public float Pos_Z;

        public Bitmap _bmp;
        public int _Width;
        public int _Height;
    }
    #endregion
    public class BitmapInfo
    {
        public int FlowID;
        public Bitmap bitmap = new Bitmap(640, 480);
        public int num;
        //public bool m_inProcessing = false;   //�Ƿ����ڴ�����
        public bool m_processed = false;  //�Ƿ��ѽ���̎��
    }

    public class IPInfo
    {
        public IPInfo(string IP, string MAC)
        {
            _IP = IP;
            _MAC = MAC;
        }
        public string _IP;
        public string _MAC;
        public int _WorkPort;
    }

}