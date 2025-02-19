﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Translator;
using Newtonsoft.Json;
using System.IO;

namespace BooruDatasetTagManager
{
    static class Program
    {
        /// <summary>
        /// Главная точка входа для приложения.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            tools = new TextTool(Application.StartupPath);
            Settings = new AppSettings(Application.StartupPath);
            #region waitForm
            Form f_wait = new Form();
            f_wait.Width = 300;
            f_wait.Height = 100;
            f_wait.FormBorderStyle = FormBorderStyle.FixedDialog;
            f_wait.ControlBox = false;
            f_wait.StartPosition = FormStartPosition.CenterScreen;
            Label mes = new Label();
            mes.Text = "Please wait while the tags are loading.\nWhen changing csv or txt files,\nthe initial loading of tags may take a long time.";
            mes.Location = new System.Drawing.Point(10, 10);
            mes.AutoSize = true;
            f_wait.Controls.Add(mes);
            
            f_wait.Shown += async (o, i) =>
            {
                await Task.Run(() =>
                {
                    string tagsDir = Path.Combine(Application.StartupPath, "Tags");
                    if(!Directory.Exists(tagsDir))
                        Directory.CreateDirectory(tagsDir);
                    string translationsDir = Path.Combine(Application.StartupPath, "Translations");
                    if(!Directory.Exists(translationsDir))
                        Directory.CreateDirectory(translationsDir);
                    string tagFile = Path.Combine(tagsDir, "List.tdb");
                    TagsList = TagsDB.LoadFromTagFile(tagFile);
                    if (TagsList.IsNeedUpdate(tagsDir))
                    {
                        TagsList.LoadCSVFromDir(tagsDir);
                        TagsList.SaveTags(tagFile);
                    }
                    TagsList.LoadTranslation(Path.Combine(translationsDir, Settings.TranslationLanguage + ".txt"));
                });
                f_wait.Close();
            };
            f_wait.ShowDialog();
            #endregion
            //string tagsDir = Path.Combine(Application.StartupPath, "tags");
            //string tagFile = Path.Combine(tagsDir, "list.tdb");



            Application.Run(new Form1());
        }

        public static DatasetManager DataManager;

        public static TextTool tools;

        public static AppSettings Settings;

        public static TagsDB TagsList;
    }
}
