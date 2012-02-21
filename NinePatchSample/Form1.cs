using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace NinePatchSample
{
    public partial class Form1 : Form
    {
        NinePatch ninePatch = new NinePatch(Properties.Resources.btn_dropdown_normal_9);

        public Form1()
        {
            InitializeComponent();
        }

        protected override void OnResize(EventArgs e)
        {
            ninePatch.ClearCache();

            button1.BackgroundImage = ninePatch.ImageSizeOf(button1.Width, button1.Height);
            button2.BackgroundImage = ninePatch.ImageSizeOf(button2.Width, button2.Height);
            button3.BackgroundImage = ninePatch.ImageSizeOf(button3.Width, button3.Height);

            base.OnResize(e);
        }
    }
}
