using CLRProfiler;
using nanoFramework.Tools.NanoProfiler.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.Metrics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace nanoFramework.Tools.NanoProfiler.Services
{
    public class BucketProvider
    {
        public Bucket[] GetBuckets()
        {
            var scaleFactor = 2;
            Bucket[] buckets = new Bucket[] { };
            int count = 0;
            int startSize = 8;
            double minSize;
            for (minSize = startSize; minSize < int.MaxValue; minSize *= scaleFactor)
            {
                count++;
            }

            buckets = new Bucket[count - 1];
            minSize = startSize;
            for (int i = 0; i < buckets.Length; i++)
            {
                buckets[i].minSize = (int)Math.Round(minSize);
                minSize *= scaleFactor;
                buckets[i].maxSize = (int)Math.Round(minSize) - 1;
                buckets[i].typeDescToSizeCount = new Dictionary<TypeDesc, SizeCount>();
                buckets[i].selected = false;
            }

            return buckets;
        }
        public Bucket[] GetBucketsByScaleFactor(double scaleFactor)
        {
            Bucket[] buckets;
            int count = 0;
            int startSize = 8;
            double minSize;
            for (minSize = startSize; minSize < int.MaxValue; minSize *= scaleFactor)
            {
                count++;
            }

            buckets = new Bucket[count - 1];
            minSize = startSize;
            for (int i = 0; i < buckets.Length; i++)
            {
                buckets[i].minSize = (int)Math.Round(minSize);
                minSize *= scaleFactor;
                buckets[i].maxSize = (int)Math.Round(minSize) - 1;
                buckets[i].typeDescToSizeCount = new Dictionary<TypeDesc, SizeCount>();
                buckets[i].selected = false;
            }

            return buckets;
        }


    }


    public class TypeDescProvider
    {

        static System.Drawing.Color[] colors = new System.Drawing.Color[16];

        static System.Drawing.Color[] firstColors =
{
            System.Drawing.Color.Red,
            System.Drawing.Color.Yellow,
            System.Drawing.Color.Green,
            System.Drawing.Color.Cyan,
            System.Drawing.Color.Blue,
            System.Drawing.Color.Magenta,
        };

        void AddToBuckets(TypeDesc t, int size, int count, Bucket[] buckets)
        {
            for (int i = 0; i < buckets.Length; i++)
            {
                if (buckets[i].minSize <= size && size <= buckets[i].maxSize)
                {
                    ulong totalSize = (ulong)size * (ulong)count;
                    buckets[i].totalSize += totalSize;
                    SizeCount sizeCount;
                    if (!buckets[i].typeDescToSizeCount.TryGetValue(t, out sizeCount))
                    {
                        sizeCount = new SizeCount();
                        buckets[i].typeDescToSizeCount[t] = sizeCount;
                    }
                    sizeCount.size += totalSize;
                    sizeCount.count += count;
                    break;
                }
            }
        }

  

        public ArrayList GetSortedTypeDescArrayList(int length, int[] typeSizeStacktraceToCount, StacktraceTable stacktraceTable, string[] typeName, Bucket[] buckets, ref TypeDesc[] typeIndexToTypeDesc )
        {
          
            var totalSize = 0UL;
            var totalCount = 0L;

            if (typeIndexToTypeDesc == null)
            {
                typeIndexToTypeDesc = new TypeDesc[length];
            }
            else
            {
                foreach (TypeDesc t in typeIndexToTypeDesc)
                {
                    if (t != null)
                    {
                        t.totalSize = 0;
                        t.count = 0;
                    }
                }
            }

            for (int i = 0; i < typeSizeStacktraceToCount.Length; i++)
            {
                int count = typeSizeStacktraceToCount[i];
                if (count == 0)
                {
                    continue;
                }

                int[] stacktrace = stacktraceTable.IndexToStacktrace(i);
                int typeIndex = stacktrace[0];
                int size = stacktrace[1];

                TypeDesc t = (TypeDesc)typeIndexToTypeDesc[typeIndex];
                if (t == null)
                {
                    t = new TypeDesc(typeName[typeIndex]);
                    typeIndexToTypeDesc[typeIndex] = t;
                }
                t.totalSize += (ulong)size * (ulong)count;
                t.count += count;

                totalSize += (ulong)size * (ulong)count;
                totalCount += count;

                AddToBuckets(t, size, count, buckets);
            }

            var sortedTypeTable = new ArrayList();
            foreach (TypeDesc t in typeIndexToTypeDesc)
            {
                if (t != null)
                {
                    sortedTypeTable.Add(t);
                }
            }


            AddColors(typeIndexToTypeDesc);
            TrimEmptyBuckets(buckets);
            return sortedTypeTable;
        }
        System.Drawing.Color MixColor(System.Drawing.Color a, System.Drawing.Color b)
        {
            int R = (a.R + b.R) / 2;
            int G = (a.G + b.G) / 2;
            int B = (a.B + b.B) / 2;

            return System.Drawing.Color.FromArgb(R, G, B);
        }
        static void GrowColors()
        {
            System.Drawing.Color[] newColors = new System.Drawing.Color[2 * colors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                newColors[i] = colors[i];
            }

            colors = newColors;
        }
        private TypeDesc FindSelectedType(TypeDesc[] typeIndexToTypeDesc)
        {
            var sortedTypeTable = new ArrayList();
            foreach (TypeDesc t in typeIndexToTypeDesc)
            {
                if (t != null)
                {
                    sortedTypeTable.Add(t);
                }
            }

            sortedTypeTable.Sort();

            foreach (TypeDesc t in sortedTypeTable)
            {
                if (t.selected)
                {
                    return t;
                }
            }

            return null;
        }

        private void AddColors(TypeDesc[] typeIndexToTypeDesc)
        {
            int count = 0;

            bool anyTypeSelected = FindSelectedType(typeIndexToTypeDesc) != null;

            var sortedTypeTable = new ArrayList();
            foreach (TypeDesc t in typeIndexToTypeDesc)
            {
                if (t != null)
                {
                    sortedTypeTable.Add(t);
                }
            }

            sortedTypeTable.Sort();


            foreach (TypeDesc t in sortedTypeTable)
            {
                if (count >= colors.Length)
                {
                    GrowColors();
                }

                if (count < firstColors.Length)
                {
                    colors[count] = firstColors[count];
                }
                else
                {
                    colors[count] = MixColor(colors[count - firstColors.Length], colors[count - firstColors.Length + 1]);
                }

                t.color = colors[count];
                if (anyTypeSelected)
                {
                    t.color = MixColor(colors[count], System.Drawing.Color.White);
                }

                t.brush = new SolidBrush(t.color);
                t.pen = new System.Drawing.Pen(t.brush);
                count++;
            }
        }


        void TrimEmptyBuckets(Bucket[] buckets)
        {
            int lo = 0;
            for (int i = 0; i < buckets.Length - 1; i++)
            {
                if (buckets[i].totalSize != 0 || buckets[i + 1].totalSize != 0)
                {
                    break;
                }

                lo++;
            }
            int hi = buckets.Length - 1;
            for (int i = buckets.Length - 1; i >= 0; i--)
            {
                if (buckets[i].totalSize != 0)
                {
                    break;
                }

                hi--;
            }
            if (lo <= hi)
            {
                Bucket[] newBuckets = new Bucket[hi - lo + 1];
                for (int i = lo; i <= hi; i++)
                {
                    newBuckets[i - lo] = buckets[i];
                }

                buckets = newBuckets;
            }
        }
    }
}
