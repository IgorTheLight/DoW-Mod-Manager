using System;
using System.Windows.Forms;

namespace DoW_DE_Nod_Manager
{
    public partial class DialogueForm : Form
    {
        readonly string exeORmods;

        public DialogueForm(string message, string title, string exeORmods)
        {
            InitializeComponent();

            this.exeORmods = exeORmods;

            dmessageLabel.Text = message;   // Make sure that text is not longer than 68 lines and each line is no longer than 300 characters!
            Text = title;
        }

        void OKButton_Click(object sender, EventArgs e)
        {
            if (exeORmods == "exe")
                DownloadHelper.DownloadExe();
            else if (exeORmods == "mods")
                DownloadHelper.DownloadModlist();
            else
                ThemedMessageBox.Show("You should choose either \"exe\" or \"mods\"!", "Error!");

            Close();
        }

        void CancelButton_Click(object sender, EventArgs e)
        {
            Close();
        }
    }

    /// <summary>
    /// A custom DialogueBox helper.
    /// </summary>
    public static class ThemedDialogueBox
    {
        public static DialogResult Show(string message, string title = " ", string exeORmods = "exe")
        {
            // "using" construct ensures the resources are freed when form is closed
            using (DialogueForm form = new DialogueForm(message, title, exeORmods))
            {
                return form.ShowDialog();
            }
        }
    }
}
