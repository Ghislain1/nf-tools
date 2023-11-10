////
// Copyright (c) .NET Foundation and Contributors.
// See LICENSE file in the project root for full license information.
////

using CLRProfiler;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveCharts;
using LiveCharts.Configurations;
using LiveCharts.Wpf;
using nanoFramework.Tools.NanoProfiler.CLRProfiler;
using nanoFramework.Tools.NanoProfiler.Models;
using nanoFramework.Tools.NanoProfiler.Services;
using nanoFramework.Tools.NanoProfiler.Views;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;

namespace nanoFramework.Tools.NanoProfiler.ViewModels
{
    public partial class HistogramViewModel : ObservableObject
    {
        #region Observable Properties     

        [ObservableProperty]
        private ObservableCollection<string> _bucketsLabels = new();

        [ObservableProperty]
        private SeriesCollection _seriesCollection = new SeriesCollection();

        [ObservableProperty]
        private ChartValues<BucketDataModel> _bucketsValues = new ChartValues<BucketDataModel>();

        [ObservableProperty]
        private ObservableCollection<double> _verticalScaleList;

        [ObservableProperty]
        private double _verticalScaleSelectedValue;

        [ObservableProperty]
        private ObservableCollection<string> _horizontalScaleList;

        [ObservableProperty]
        private string _horizontalScaleSelectedValue;

        [ObservableProperty]
        private ChartPoint _selectedChartPoint = new();

        [ObservableProperty]
        private string _title;
        #endregion


        #region Properties
        private List<TypeDescModel> listValues = new List<TypeDescModel>();
        private Dictionary<int, List<TypeDescModel>> originalDictionary = new Dictionary<int, List<TypeDescModel>>();
        private Dictionary<int, List<TypeDescModel>> convertedDictionary = new Dictionary<int, List<TypeDescModel>>();
        private readonly BucketProvider bucketProvider = new BucketProvider();
        private readonly TypeDescProvider typeDescProvider = new TypeDescProvider();

        

        Bucket[] buckets = new Bucket[] { };
        double currentScaleFactor;
        public Histogram histogram { get; set; }
        private string[] typeName;
    

        ulong totalSize;
        int totalCount;

        TypeDesc[] typeIndexToTypeDesc;

        ArrayList sortedTypeTable;
        bool initialized = false;

        private int selectedPositionOfTheBucket;

        #endregion

        public HistogramViewModel(Histogram histogram, string title)
        {
            this.histogram = histogram;
            Title = title;
            typeName = this.histogram.readNewLog.typeName;

            SetComboValues();

            SetHistogram();
        }



        private void SetComboValues()
        {
            VerticalScaleList = new ObservableCollection<double>() { 1, 2, 5, 10, 20, 50, 100, 200, 500, 1000, 2000, 5000, 10000, 20000 };
            VerticalScaleSelectedValue = 10;

            HorizontalScaleList = new ObservableCollection<string>() { "Coarse", "Medium", "Fine", "Very Fine" };
            HorizontalScaleSelectedValue = "Coarse";
        }

        public void SetHistogram()
        {
            graphPanel_Paint();

        }

        partial void OnVerticalScaleSelectedValueChanged(double value)
        {
            //scaleFactor = VerticalScaleSelectedValue;
            //SetHistogram();
        }
        partial void OnHorizontalScaleSelectedValueChanged(string value)
        {
            switch (HorizontalScaleSelectedValue)
            {
                case "Coarse":
                    scaleFactor = 2.0;
                    break;
                case "Medium":
                    scaleFactor = Math.Sqrt(2.0);
                    break;
                case "Fine":
                    scaleFactor = Math.Pow(2.0, 0.25);
                    break;
                case "Very Fine":
                    scaleFactor = Math.Pow(2.0, 0.125);
                    break;
            }
            SetHistogram();
        }

        Bucket bucketClicked;
        #region Commands
        [RelayCommand]
        private void ShowWhoAllocated(object selectedBucket)
        {
            bucketClicked = BucketsValues[selectedPositionOfTheBucket].FullBucket;

            var typesInBucketClicked = bucketClicked.typeDescToSizeCount.Count;

            string title;
            List<TypeDesc> selectedTypes = FindSelectedTypeInSelectedBucket();

            Histogram selectedHistogram = new Histogram(histogram.readNewLog);
            if (selectedTypes != null && selectedTypes.Count > 0)
            {
                foreach (TypeDesc selectedType in selectedTypes)
                {
                    if (selectedType == null)
                    {
                        title = "Allocation Graph";
                        selectedHistogram = histogram;
                    }
                    else
                    {
                        int minSize = 0;
                        int maxSize = int.MaxValue;
                        foreach (Bucket b in buckets)
                        {
                            if (b.selected)
                            {
                                minSize = b.minSize;
                                maxSize = b.maxSize;
                            }
                        }
                        title = string.Format("Allocation Graph for {0} objects", selectedType.typeName);
                        if (minSize > 0)
                        {
                            title += string.Format(" of size between {0:n0} and {1:n0} bytes", minSize, maxSize);
                        }

                        for (int i = 0; i < histogram.typeSizeStacktraceToCount.Length; i++)
                        {
                            int count = histogram.typeSizeStacktraceToCount[i];
                            if (count > 0)
                            {
                                int[] stacktrace = histogram.readNewLog.stacktraceTable.IndexToStacktrace(i);
                                int typeIndex = stacktrace[0];
                                int size = stacktrace[1];

                                if (minSize <= size && size <= maxSize)
                                {
                                    TypeDesc t = (TypeDesc)typeIndexToTypeDesc[typeIndex];

                                    if (t == selectedType)
                                    {
                                        selectedHistogram.AddObject(i, count);
                                    }
                                }
                            }
                        }
                    }
                }
            }



            Graph graph = selectedHistogram.BuildAllocationGraph(new FilterForm());

            //WinForms.CLRProfiler.GraphViewForm graphViewForm = new WinForms.CLRProfiler.GraphViewForm(graph, title);
            //graphViewForm.Show();


            GraphViewModel viewModel = new GraphViewModel(graph);
            GraphView graphView = new GraphView();
            graphView.DataContext = viewModel;
            graphView.Show();

        }


        [RelayCommand]
        private void DrillDown(ChartPoint chartPointSelected)
        {
            selectedPositionOfTheBucket = chartPointSelected.Key;
        }
        #endregion

        private void graphPanel_Paint()
        {
            initialized = false;

            if (histogram == null || typeName == null)
            {
                return;
            }

            BuildSizeRangesAndTypeTable(histogram.typeSizeStacktraceToCount);

           // ColorTypes();

            DrawBuckets();

            initialized = true;
        }

        int verticalScale = 0;


        string FormatSize(ulong size)
        {
            double w = size;
            string byteString = "bytes";
            if (w >= 1024)
            {
                w /= 1024;
                byteString = "kB";
            }
            if (w >= 1024)
            {
                w /= 1024;
                byteString = "MB";
            }
            if (w >= 1024)
            {
                w /= 1024;
                byteString = "GB";
            }
            string format = "{0:f0} {1}";
            if (w < 10)
            {
                format = "{0:f1} {1}";
            }

            return string.Format(format, w, byteString);
        }

        private void DrawBuckets()
        {
            originalDictionary = new Dictionary<int, List<TypeDescModel>>();
            convertedDictionary = new Dictionary<int, List<TypeDescModel>>();
            BucketsValues = new ChartValues<BucketDataModel> { };
            BucketsLabels = new ObservableCollection<string>();
            SeriesCollection = new();

            StackedColumnSeries columnSeries = new StackedColumnSeries();

            bool noBucketSelected = true;
            foreach (Bucket b in buckets)
            {
                if (b.selected)
                {
                    noBucketSelected = false;
                    break;
                }
            }

            double totalsizeCount = 0;
            using (System.Drawing.Brush blackBrush = new SolidBrush(System.Drawing.Color.Black))
            {
                int bucketPosition = 0;
                //int x = leftMargin;
                foreach (Bucket b in buckets)
                {
                    BucketsValues.Add(new BucketDataModel()
                    {
                        BucketPosition = bucketPosition,
                        FullBucket = b
                    });
                    string label = string.Empty;
                    if (HorizontalScaleSelectedValue.Equals("Coarse"))
                    {
                        label = "< " + FormatSize((ulong)b.maxSize + 1) + $"{Environment.NewLine}";
                        label += FormatSize(b.totalSize) + $"{Environment.NewLine}";
                        label += "(";
                        label += string.Format("{0:f2}%", 100.0 * b.totalSize / totalSize);
                        label += ")";
                    }
                    else
                    {
                        double res = Math.Round(100.0 * b.totalSize / totalSize, 2);
                        label += $"{res}{Environment.NewLine}";
                        label += $"%";
                    }

                    System.Drawing.Brush brush = new SolidBrush(System.Drawing.Color.Transparent);

                    listValues = new List<TypeDescModel>();
                    foreach (KeyValuePair<TypeDesc, SizeCount> d in b.typeDescToSizeCount)
                    {
                        TypeDesc t = d.Key;

                        brush = t.brush;
                        if (t.selected && (b.selected || noBucketSelected))
                        {
                            brush = blackBrush;
                        }
                        var buckDet = new BucketDataModel1()
                        {
                            SectionValue = d.Value.size   //t.totalSize
                        };

                        listValues.Add(new TypeDescModel()
                        {
                            TypeDesc = t,
                            ValueSize = d.Value.size,
                            BucketTotalSize = b.totalSize
                        });
                        totalsizeCount += d.Value.size;
                    }
                    originalDictionary.Add(bucketPosition, listValues);

                    System.Drawing.Color drawingColor = ((SolidBrush)brush).Color;
                    System.Windows.Media.Color wpfColor = System.Windows.Media.Color.FromArgb(drawingColor.A, drawingColor.R, drawingColor.G, drawingColor.B);

                    BucketsLabels.Add(label);

                    //x += bucketWidth + gap;
                    bucketPosition++;
                }
            }

            //  Now convert to converted dictionary
            int maxLength = originalDictionary.Values.Any() ? originalDictionary.Values.Max(list => list.Count) : 0;

            for (int position = 0; position < maxLength; position++)
            {
                List<TypeDescModel> values = new List<TypeDescModel>();

                foreach (var kvp in originalDictionary)
                {
                    List<TypeDescModel> list = kvp.Value;
                    if (position < list.Count)
                    {
                        values.Add(list[position]);
                    }
                    else
                        values.Add(null);
                }

                convertedDictionary[position] = values;
            }

            foreach (KeyValuePair<int, List<TypeDescModel>> item in convertedDictionary)
            {
                IChartValues values = new ChartValues<TypeDescModel>();
                if (item.Value != null && item.Value.Count > 0)
                {
                    foreach (TypeDescModel typeDescModel in item.Value)
                    {
                        if (typeDescModel != null)
                        {
                            values.Add(typeDescModel);
                        }
                        else
                        {
                            values.Add(new TypeDescModel());
                        }
                    }
                }

                var config = new CartesianMapper<TypeDescModel>()
                      .X((value, index) => index)
                      .Y((value, index) => value != null ? Math.Round((100.0 * value.ValueSize / totalsizeCount), 2) : 0d)
                      ;

                var stackedColumSeries = new StackedColumnSeries
                {
                    Configuration = config,
                    Values = values,
                    DataLabels = false
                };

                SeriesCollection.Add(stackedColumSeries);
            }

        }
 

        private List<TypeDesc> FindSelectedTypeInSelectedBucket()
        {
            List<TypeDesc> listReturning = new List<TypeDesc>();

            foreach (KeyValuePair<TypeDesc, SizeCount> item in bucketClicked.typeDescToSizeCount)
            {
                listReturning.Add(item.Key);
            }
            return listReturning;
        }

 

        void BuildSizeRangesAndTypeTable(int[] typeSizeStacktraceToCount)
        {
            BuildBuckets();

            totalSize = 0;
            totalCount = 0;

       
    
            sortedTypeTable = typeDescProvider.GetSortedTypeDescArrayList(histogram.readNewLog.typeName.Length, typeSizeStacktraceToCount, histogram.readNewLog.stacktraceTable, typeName, buckets, ref typeIndexToTypeDesc);
            if (totalSize == 0)
            {
                totalSize = 1;
            }

            if (totalCount == 0)
            {
                totalCount = 1;
            }

        }

        private double scaleFactor;
        void BuildBuckets()
        {
            if (currentScaleFactor == scaleFactor)
            {
                for (int i = 0; i < buckets.Length; i++)
                {
                    buckets[i].typeDescToSizeCount = new Dictionary<TypeDesc, SizeCount>();
                    buckets[i].totalSize = 0;
                }
                return;
            }

            currentScaleFactor = scaleFactor;
            buckets = bucketProvider.GetBucketsByScaleFactor(scaleFactor);



        }

 
 
    }
}