﻿#pragma checksum "..\..\..\..\UserControls\ImageFilterUserControl.xaml" "{ff1816ec-aa5e-4d10-87f7-6f4963833460}" "BD9D34788AD2EAFDF733CE8867B53AC76673E6EA"
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Media.TextFormatting;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Shell;
using gDiamondViewer.UserControls;


namespace gDiamondViewer.UserControls {
    
    
    /// <summary>
    /// ImageFilterUserControl
    /// </summary>
    public partial class ImageFilterUserControl : System.Windows.Controls.UserControl, System.Windows.Markup.IComponentConnector {
        
        
        #line 29 "..\..\..\..\UserControls\ImageFilterUserControl.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.Slider SliderBrightness;
        
        #line default
        #line hidden
        
        
        #line 49 "..\..\..\..\UserControls\ImageFilterUserControl.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.Slider SliderContrast;
        
        #line default
        #line hidden
        
        
        #line 83 "..\..\..\..\UserControls\ImageFilterUserControl.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.Slider SliderGMin;
        
        #line default
        #line hidden
        
        
        #line 103 "..\..\..\..\UserControls\ImageFilterUserControl.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.Slider SliderGMax;
        
        #line default
        #line hidden
        
        
        #line 123 "..\..\..\..\UserControls\ImageFilterUserControl.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.Slider SliderFMin;
        
        #line default
        #line hidden
        
        
        #line 143 "..\..\..\..\UserControls\ImageFilterUserControl.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.Slider SliderFMax;
        
        #line default
        #line hidden
        
        
        #line 163 "..\..\..\..\UserControls\ImageFilterUserControl.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.Slider SliderGamma;
        
        #line default
        #line hidden
        
        private bool _contentLoaded;
        
        /// <summary>
        /// InitializeComponent
        /// </summary>
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "4.0.0.0")]
        public void InitializeComponent() {
            if (_contentLoaded) {
                return;
            }
            _contentLoaded = true;
            System.Uri resourceLocater = new System.Uri("/gDiamondViewer;component/usercontrols/imagefilterusercontrol.xaml", System.UriKind.Relative);
            
            #line 1 "..\..\..\..\UserControls\ImageFilterUserControl.xaml"
            System.Windows.Application.LoadComponent(this, resourceLocater);
            
            #line default
            #line hidden
        }
        
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "4.0.0.0")]
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Design", "CA1033:InterfaceMethodsShouldBeCallableByChildTypes")]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1800:DoNotCastUnnecessarily")]
        void System.Windows.Markup.IComponentConnector.Connect(int connectionId, object target) {
            switch (connectionId)
            {
            case 1:
            this.SliderBrightness = ((System.Windows.Controls.Slider)(target));
            
            #line 41 "..\..\..\..\UserControls\ImageFilterUserControl.xaml"
            this.SliderBrightness.ValueChanged += new System.Windows.RoutedPropertyChangedEventHandler<double>(this.SliderBrightness_OnValueChanged);
            
            #line default
            #line hidden
            return;
            case 2:
            this.SliderContrast = ((System.Windows.Controls.Slider)(target));
            
            #line 61 "..\..\..\..\UserControls\ImageFilterUserControl.xaml"
            this.SliderContrast.ValueChanged += new System.Windows.RoutedPropertyChangedEventHandler<double>(this.SliderContrast_OnValueChanged);
            
            #line default
            #line hidden
            return;
            case 3:
            this.SliderGMin = ((System.Windows.Controls.Slider)(target));
            
            #line 95 "..\..\..\..\UserControls\ImageFilterUserControl.xaml"
            this.SliderGMin.ValueChanged += new System.Windows.RoutedPropertyChangedEventHandler<double>(this.SliderGMin_OnValueChanged);
            
            #line default
            #line hidden
            return;
            case 4:
            this.SliderGMax = ((System.Windows.Controls.Slider)(target));
            
            #line 115 "..\..\..\..\UserControls\ImageFilterUserControl.xaml"
            this.SliderGMax.ValueChanged += new System.Windows.RoutedPropertyChangedEventHandler<double>(this.SliderGMax_OnValueChanged);
            
            #line default
            #line hidden
            return;
            case 5:
            this.SliderFMin = ((System.Windows.Controls.Slider)(target));
            
            #line 135 "..\..\..\..\UserControls\ImageFilterUserControl.xaml"
            this.SliderFMin.ValueChanged += new System.Windows.RoutedPropertyChangedEventHandler<double>(this.SliderFMin_OnValueChanged);
            
            #line default
            #line hidden
            return;
            case 6:
            this.SliderFMax = ((System.Windows.Controls.Slider)(target));
            
            #line 155 "..\..\..\..\UserControls\ImageFilterUserControl.xaml"
            this.SliderFMax.ValueChanged += new System.Windows.RoutedPropertyChangedEventHandler<double>(this.SliderFMax_OnValueChanged);
            
            #line default
            #line hidden
            return;
            case 7:
            this.SliderGamma = ((System.Windows.Controls.Slider)(target));
            
            #line 174 "..\..\..\..\UserControls\ImageFilterUserControl.xaml"
            this.SliderGamma.ValueChanged += new System.Windows.RoutedPropertyChangedEventHandler<double>(this.SliderGamma_OnValueChanged);
            
            #line default
            #line hidden
            return;
            }
            this._contentLoaded = true;
        }
    }
}

