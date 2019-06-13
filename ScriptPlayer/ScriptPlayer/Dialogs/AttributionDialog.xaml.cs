﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using System.Windows.Shapes;

namespace ScriptPlayer.Dialogs
{
    /// <summary>
    /// Interaction logic for AttributionDialog.xaml
    /// </summary>
    public partial class AttributionDialog : Window
    {
        public static readonly DependencyProperty AttributionsProperty = DependencyProperty.Register(
            "Attributions", typeof(List<AttributionEntry>), typeof(AttributionDialog), new PropertyMetadata(default(List<AttributionEntry>)));

        public List<AttributionEntry> Attributions
        {
            get { return (List<AttributionEntry>) GetValue(AttributionsProperty); }
            set { SetValue(AttributionsProperty, value); }
        }

        public AttributionDialog()
        {
            CreateAttributions();
            InitializeComponent();
        }

        private void CreateAttributions()
        {
            Attributions = new List<AttributionEntry>
            {
                new ExtendedAttributionEntry("qDot", "Buttplug", new Link("Patreon", "https://www.patreon.com/qdot")),
                new ExtendedAttributionEntry("BlackSphereFollower", "Help with BLE"),
                new ExtendedAttributionEntry("Funjack", "Formats and GFX", new Link("Github", "https://github.com/funjack/")),
                new ExtendedAttributionEntry("RickNLX", "Samsung VR"),
                new ExtendedAttributionEntry("Net005", "Zoom Player"),
                new ExtendedAttributionEntry("Milovana", "Community", new Link("Homepage", "https://milovana.com/")),
                new ExtendedAttributionEntry("RTS", "Community", new Link("Homepage", "http://realtouchscripts.com/")),
                new AttributionEntry("And a whole lot of people who probably don't want anything to do with this smut :)")
            };
        }

        private void LinkButton_OnHandler(object sender, RoutedEventArgs e)
        {
            if (!(((Button) sender).DataContext is Link link))
                return;

            Process.Start(link.Url);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    public class AttributionEntry
    {
        public string Creator { get; set; }

        public AttributionEntry(string creator)
        {
            Creator = creator;
        }
    }

    public class ExtendedAttributionEntry : AttributionEntry
    {
        public string Thing { get; set; }

        public List<Link> Links { get; set; }

        public ExtendedAttributionEntry(string creator, string thing, params Link[] links) : base(creator)
        {
            Links = links.ToList();
            Thing = thing;
        }
    }

    public class Link
    {
        public string Title { get; set; }

        public string Url { get; set; }

        public Link(string title, string url)
        {
            Title = title;
            Url = url;
        }
    }
}
