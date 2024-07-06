using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Foldda.Automation.HandlerDevKit
{
    static class Extensions
    {
        // https://stackoverflow.com/questions/2367718/automating-the-invokerequired-code-pattern

        public static void InvokeIfRequired(this Control control, MethodInvoker action)
        {
            if (control.InvokeRequired)
            {
                control.BeginInvoke(action);
            }
            else
            {
                action();
            }

        }

        // https://stackoverflow.com/questions/87795/how-to-prevent-flickering-in-listview-when-updating-a-single-listviewitems-text
        public static void DoubleBuffered(this Control control, bool enable)
        {
            var doubleBufferPropertyInfo = control.GetType().GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            doubleBufferPropertyInfo.SetValue(control, enable, null);
        }

        public static void AddContextMenu(this RichTextBox rtb)
        {
            if (rtb.ContextMenuStrip == null)
            {
                ContextMenuStrip cms = new ContextMenuStrip { ShowImageMargin = false };
                ToolStripMenuItem tsmiCopy = new ToolStripMenuItem("Copy");
                tsmiCopy.Click += (sender, e) => rtb.Copy();
                cms.Items.Add(tsmiCopy);
                //similarily can also have items like-
                //.. tsmiCut.Click += (sender, e) => rtb.Cut();
                //.. tsmiPaste.Click += (sender, e) => rtb.Paste();
                rtb.ContextMenuStrip = cms;
            }
        }

        public static void HighlightText(this RichTextBox rtb, string word, Color backColor)
        {
            if (string.IsNullOrEmpty(word)) { return; }

            int s_start = rtb.SelectionStart, startIndex = 0, index;
            try
            {
                while ((index = rtb.Text.IndexOf(word, startIndex)) != -1)
                {
                    rtb.Select(index, word.Length);
                    //rtb.SelectionColor = color;
                    rtb.SelectionBackColor = backColor;

                    startIndex = index + word.Length;
                }

                rtb.SelectionStart = s_start;
                rtb.SelectionLength = 0;
                rtb.SelectionColor = Color.Black;
            }
            catch (Exception e)
            {
                throw new Exception($"{e.Message} \n{e.StackTrace}- startIndex = {startIndex}");
            }
        }
    }
}
