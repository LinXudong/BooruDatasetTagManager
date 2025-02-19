﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BooruDatasetTagManager
{
    public class AutoCompleteTextBox : TextBox
    {
        private ListBox _listBox;
        private bool _isAdded;
        private String[] _values;
        private String _formerValue = String.Empty;
        private AutocompleteMode _mode = AutocompleteMode.StartWithAndContains;

        public AutoCompleteTextBox()
        {
            InitializeComponent();
            ResetListBox();
        }

        private void InitializeComponent()
        {
            _listBox = new ListBox();
            this.KeyDown += this_KeyDown;
            this.KeyUp += this_KeyUp;
            this.PreviewKeyDown += AutoCompleteTextBox_PreviewKeyDown;
            _listBox.MouseDoubleClick += AutoCompleteTextBox_MouseClick;
        }

        private void AutoCompleteTextBox_MouseClick(object sender, MouseEventArgs e)
        {
            if (_listBox.Visible)
            {
                Text = _listBox.SelectedItem.ToString();
                ResetListBox();
                _formerValue = Text;
                //_parent.Focus();
                //Parent.Parent.Focus();
                _listBox.Parent.Focus();
                //this.Select(this.Text.Length, 0);
            }
        }

        private void AutoCompleteTextBox_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Tab)
            {
                if (_listBox.Visible)
                {
                    Text = _listBox.SelectedItem.ToString();
                    ResetListBox();
                    _formerValue = Text;
                    //this.Select(this.Text.Length, 0);
                }
            }
            else if (e.KeyCode == Keys.Escape)
            {
                ResetListBox();
            }
        }

        public void SetAutocompleteMode(AutocompleteMode mode)
        {
            _mode = mode;
        }

        private void ShowListBox()
        {
            if (!_isAdded)
            {
                //Parent.Controls.Add(_listBox);
                //_parent.Controls.Add(_listBox);
                //_listBox.Parent = Parent;

                Parent.Parent.Controls.Add(_listBox);
                _isAdded = true;
            }

            //int gridHeight = Parent.Parent.Height;
            //int upMaxSize = Parent.Top;
            //int downMaxSize = gridHeight - Parent.Top + Parent.Height;

            //if (upMaxSize > downMaxSize)
            //{

            //}


            //_listBox.Left = Left;
            //_listBox.Top = Parent.Top + Parent.Height;
            _listBox.Visible = true;
            _listBox.BringToFront();
            //_listBox.Focus();
        }

        //private Control GetRootControl(Control control)
        //{
        //    if(control.Parent!=null)
        //        return GetRootControl(control.Parent);
        //    else
        //        return control;
        //}

        public void ResetListBox()
        {
            _listBox.Visible = false;
        }

        private void this_KeyUp(object sender, KeyEventArgs e)
        {
            UpdateListBox();
        }

        private void this_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Enter:
                case Keys.Tab:
                    {
                        if (_listBox.Visible)
                        {
                            Text = _listBox.SelectedItem.ToString();
                            ResetListBox();
                            _formerValue = Text;
                            this.Select(this.Text.Length, 0);
                            e.Handled = true;
                        }
                        break;
                    }
                case Keys.Down:
                    {
                        if ((_listBox.Visible) && (_listBox.SelectedIndex < _listBox.Items.Count - 1))
                            _listBox.SelectedIndex++;
                        e.Handled = true;
                        break;
                    }
                case Keys.Up:
                    {
                        if ((_listBox.Visible) && (_listBox.SelectedIndex > 0))
                            _listBox.SelectedIndex--;
                        e.Handled = true;
                        break;
                    }


            }
        }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Tab:
                    if (_listBox.Visible)
                        return true;
                    else
                        return false;
                default:
                    return base.IsInputKey(keyData);
            }
        }

        private void UpdateListBox()
        {
            if (Text == _formerValue)
                return;

            _formerValue = this.Text;
            string word = this.Text;

            if (_values != null && word.Length > 2)
            {
                string[] matches = null;
                if (_mode == AutocompleteMode.StartWith)
                {
                    matches = Array.FindAll(_values, x => (x.ToLower().StartsWith(word.ToLower())));
                }
                else
                {
                    matches = Array.FindAll(_values, x => (x.ToLower().StartsWith(word.ToLower())));

                }
                //matches = Array.FindAll(_values, x => (x.ToLower().Contains(word.ToLower())));
                if (matches.Length > 0)
                {
                    ShowListBox();
                    _listBox.BeginUpdate();
                    _listBox.Items.Clear();
                    Array.ForEach(matches, x => _listBox.Items.Add(x));
                    _listBox.SelectedIndex = 0;
                    _listBox.Height = 0;
                    _listBox.Width = 0;
                    Focus();

                    int upMaxSize = Parent.Top;
                    int downMaxSize = Parent.Parent.Height - (Parent.Top + Parent.Height);
                    int maxSize = 0;
                    bool isDown = false;
                    if (upMaxSize > downMaxSize)
                    {
                        maxSize = upMaxSize;
                    }
                    else
                    {
                        maxSize = downMaxSize;
                        isDown = true;
                    }
                    for (int i = 0; i < _listBox.Items.Count; i++)
                    {
                        if (i < 20 && _listBox.Height + _listBox.GetItemHeight(i) < maxSize)
                            _listBox.Height += _listBox.GetItemHeight(i);
                        _listBox.Width = this.Width;
                    }

                    //using (Graphics graphics = _listBox.CreateGraphics())
                    //{
                    //    for (int i = 0; i < _listBox.Items.Count; i++)
                    //    {
                    //        if (i < 20 && _listBox.Height < maxSize)
                    //            _listBox.Height += _listBox.GetItemHeight(i);
                    //        // it item width is larger than the current one
                    //        // set it to the new max item width
                    //        // GetItemRectangle does not work for me
                    //        // we add a little extra space by using '_'
                    //        int itemWidth = (int)graphics.MeasureString(((string)_listBox.Items[i]) + "_", _listBox.Font).Width;
                    //        _listBox.Width = (_listBox.Width < itemWidth) ? itemWidth : this.Width; ;
                    //    }
                    //}
                    if (isDown)
                    {
                        _listBox.Left = Left;
                        _listBox.Top = Parent.Top + Parent.Height;
                    }
                    else
                    {
                        _listBox.Left = Left;
                        _listBox.Top = Parent.Top - _listBox.Height;
                    }
                    _listBox.EndUpdate();
                }
                else
                {
                    ResetListBox();
                }
            }
            else
            {
                ResetListBox();
            }
        }

        public String[] Values
        {
            get
            {
                return _values;
            }
            set
            {
                _values = value;
            }
        }

        public List<String> SelectedValues
        {
            get
            {
                String[] result = Text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                return new List<String>(result);
            }
        }


        public enum AutocompleteMode
        {
            StartWith,
            StartWithAndContains
        }
    }
}
