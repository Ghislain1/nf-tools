////
// Copyright (c) .NET Foundation and Contributors.
// See LICENSE file in the project root for full license information.
////

using LiveCharts;
using LiveCharts.Configurations;
using LiveCharts.Wpf;
using nanoFramework.Tools.NanoProfiler.Models;
using System.ComponentModel;

namespace nanoFramework.Tools.NanoProfiler.Views.Controls;

public partial class HistogramTooltip : IChartTooltip
{
    private TooltipData? _data;
    public HistogramTooltip()
    {
        InitializeComponent();
        //let create a mapper so LiveCharts know how to plot our CustomerViewModel class
        var customerVmMapper = Mappers.Xy<TypeDesc>()
            .X((value, index) => index) // lets use the position of the item as X
            .Y(value => value.count); //and PurchasedItems property as Y

        //lets save the mapper globally
        Charting.For<TypeDesc>(customerVmMapper);

        DataContext = this;
    }
    public event PropertyChangedEventHandler? PropertyChanged;

    public TooltipData? Data
    {
        get { return _data; }
        set
        {
            _data = value;
            OnPropertyChanged("Data");
        }
    }

    public TooltipSelectionMode? SelectionMode { get; set; }

    protected virtual void OnPropertyChanged(string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
