using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Net;
using System.Text;
using System.Windows.Forms;

namespace shadowsocks_.net
{
    public partial class Form1 : Form
    {
        Config config;
        Server server;

        public Form1()
        {
            InitializeComponent();
        }

        private void reload(Config config)
        {
            if (server != null)
            {
                server.Stop();
            }
            server = new Server(config);
            server.Start();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            try
            {
                Config config = Config.Load();
                this.config = config;
                reload(config);
                this.Hide();
            }
            catch (FormatException)
            {
                MessageBox.Show("there is format problem");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void UpDateStatusList()
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
        }

    }
}
