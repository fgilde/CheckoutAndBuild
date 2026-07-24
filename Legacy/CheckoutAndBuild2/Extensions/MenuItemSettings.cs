using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using FG.CheckoutAndBuild2.Common.Commands;
using FG.CheckoutAndBuild2.ViewModels;

namespace FG.CheckoutAndBuild2.Extensions
{
    public class MenuItemSettings
    {
        public FontWeight FontWeight { get; set; }
        public bool IsCheckInCommand { get; set; }

        public bool IsVisibleInQuicklist { get; set; }
        public bool BindVisibility { get; set; }
        public Func<IEnumerable<ProjectViewModel>, Task<bool>> IsVisibleAsync { get; set; }
    }

}