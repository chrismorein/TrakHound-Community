﻿// Copyright (c) 2015 Feenux LLC, All Rights Reserved.

// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace TH_DataCenter.Controls
{
    /// <summary>
    /// Interaction logic for NumberDisplay.xaml
    /// </summary>
    public partial class NumberDisplay : UserControl
    {
        public NumberDisplay()
        {
            InitializeComponent();
            DataContext = this;
        }

        public string Value
        {
            get { return (string)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(string), typeof(NumberDisplay), new PropertyMetadata(null));


        public string Value_Type
        {
            get { return (string)GetValue(Value_TypeProperty); }
            set { SetValue(Value_TypeProperty, value); }
        }

        public static readonly DependencyProperty Value_TypeProperty =
            DependencyProperty.Register("Value_Type", typeof(string), typeof(NumberDisplay), new PropertyMetadata(null));


        public bool ValueIncreasing
        {
            get { return (bool)GetValue(ValueIncreasingProperty); }
            set { SetValue(ValueIncreasingProperty, value); }
        }

        public static readonly DependencyProperty ValueIncreasingProperty =
            DependencyProperty.Register("ValueIncreasing", typeof(bool), typeof(NumberDisplay), new PropertyMetadata(false));

        public bool ValueDecreasing
        {
            get { return (bool)GetValue(ValueDecreasingProperty); }
            set { SetValue(ValueDecreasingProperty, value); }
        }

        public static readonly DependencyProperty ValueDecreasingProperty =
            DependencyProperty.Register("ValueDecreasing", typeof(bool), typeof(NumberDisplay), new PropertyMetadata(false));

    }
}
