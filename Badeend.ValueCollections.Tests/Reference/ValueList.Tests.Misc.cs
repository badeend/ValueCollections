// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Badeend.ValueCollections.Tests.Reference
{
    /// <summary>
    /// Contains tests that ensure the correctness of the List class.
    /// </summary>
    public class ValueList_Tests_Insert
    {
        internal class Driver<T>
        {
            public Func<T[], IEnumerable<T>>[] CollectionGenerators { get; }

            public Driver()
            {
                CollectionGenerators = new Func<T[], IEnumerable<T>>[]
                {
                    ConstructTestList,
                    ConstructTestEnumerable,
                    ConstructLazyTestEnumerable,
                };
            }

            public IEnumerable<T> ConstructTestEnumerable(T[] items)
            {
                return items;
            }

            public IEnumerable<T> ConstructLazyTestEnumerable(T[] items)
            {
                return ConstructTestEnumerable(items)
                    .Select(item => item);
            }

            public IEnumerable<T> ConstructTestList(T[] items)
            {
                return items.ToValueList();
            }

            #region GetRange

            public void BasicGetRange(T[] items, int index, int count)
            {
                ValueList<T> list = items.ToValueList();
                ValueSlice<T> range = list.Slice(index, count);

                //ensure range is good
                for (int i = 0; i < count; i++)
                {
                    Assert.Equal(range[i], items[i + index]); //String.Format("Err_170178aqhbpa Expected item: {0} at: {1} actual: {2}", items[i + index], i, range[i])
                }

                //ensure no side effects
                for (int i = 0; i < items.Length; i++)
                {
                    Assert.Equal(list[i], items[i]); //String.Format("Err_00125698ahpap Expected item: {0} at: {1} actual: {2}", items[i], i, list[i])
                }
            }

            public void BasicSliceSyntax(T[] items, int index, int count)
            {
#if !NET462 // Don't know why the 4.6.2 build can't handle this... 4.8 works fine
                ValueList<T> list = items.ToValueList();
                ValueSlice<T> range = list[index..(index + count)];

                //ensure range is good
                for (int i = 0; i < count; i++)
                {
                    Assert.Equal(range[i], items[i + index]); //String.Format("Err_170178aqhbpa Expected item: {0} at: {1} actual: {2}", items[i + index], i, range[i])
                }

                //ensure no side effects
                for (int i = 0; i < items.Length; i++)
                {
                    Assert.Equal(list[i], items[i]); //String.Format("Err_00125698ahpap Expected item: {0} at: {1} actual: {2}", items[i], i, list[i])
                }
#endif
            }

            public void GetRangeValidations(T[] items)
            {
                //
                //Always send items.Length is even
                //
                ValueList<T> list = items.ToValueList();
                int[] bad = new int[] {  /**/items.Length,1,
                    /**/
                                    items.Length+1,0,
                    /**/
                                    items.Length+1,1,
                    /**/
                                    items.Length,2,
                    /**/
                                    items.Length/2,items.Length/2+1,
                    /**/
                                    items.Length-1,2,
                    /**/
                                    items.Length-2,3,
                    /**/
                                    1,items.Length,
                    /**/
                                    0,items.Length+1,
                    /**/
                                    1,items.Length+1,
                    /**/
                                    2,items.Length,
                    /**/
                                    items.Length/2+1,items.Length/2,
                    /**/
                                    2,items.Length-1,
                    /**/
                                    3,items.Length-2
                                };

                for (int i = 0; i < bad.Length; i++)
                {
                    AssertExtensions.Throws<ArgumentOutOfRangeException>(() =>
                    {
                        int index = bad[i];
                        int count = bad[++i];
                        list.Slice(index, count);
                    }); //"ArgumentException expected."
                }

                bad = new int[] {
                    /**/
                                    -1,-1,
                    /**/
                                    -1,0,
                    /**/
                                    -1,1,
                    /**/
                                    -1,2,
                    /**/
                                    0,-1,
                    /**/
                                    1,-1,
                    /**/
                                    2,-1
                                };

                for (int i = 0; i < bad.Length; i++)
                {
                    Assert.Throws<ArgumentOutOfRangeException>(() =>
                    {
                        int index = bad[i];
                        int count = bad[++i];
                        return list.Slice(index, count);
                    }); //"ArgumentOutOfRangeException expected."
                }
            }
            #endregion

            #region Contains

            public void BasicContains(T[] items)
            {
                ValueList<T> list = items.ToValueList();

                for (int i = 0; i < items.Length; i++)
                {
                    Assert.True(list.Contains(items[i])); //"Should contain item."
                }
            }

            public void NonExistingValues(T[] itemsX, T[] itemsY)
            {
                ValueList<T> list = itemsX.ToValueList();

                for (int i = 0; i < itemsY.Length; i++)
                {
                    Assert.False(list.Contains(itemsY[i])); //"Should not contain item"
                }
            }

            public void ContainsNullWhenReference(T[] items, T value)
            {
                if ((object)value != null)
                {
                    throw new ArgumentException("invalid argument passed to testcase");
                }

                ValueList<T> list = [value];
                Assert.True(list.Contains(value)); //"Should contain item."
            }

            #endregion
        }

        [Fact]
        public static void GetRangeTests()
        {
            Driver<int> IntDriver = new Driver<int>();
            int[] intArr1 = new int[100];
            for (int i = 0; i < 100; i++)
                intArr1[i] = i;

            IntDriver.BasicGetRange(intArr1, 50, 50);
            IntDriver.BasicGetRange(intArr1, 0, 50);
            IntDriver.BasicGetRange(intArr1, 50, 25);
            IntDriver.BasicGetRange(intArr1, 0, 25);
            IntDriver.BasicGetRange(intArr1, 75, 25);
            IntDriver.BasicGetRange(intArr1, 0, 100);
            IntDriver.BasicGetRange(intArr1, 0, 99);
            IntDriver.BasicGetRange(intArr1, 1, 1);
            IntDriver.BasicGetRange(intArr1, 99, 1);

            Driver<string> StringDriver = new Driver<string>();
            string[] stringArr1 = new string[100];
            for (int i = 0; i < 100; i++)
                stringArr1[i] = "SomeTestString" + i.ToString();

            StringDriver.BasicGetRange(stringArr1, 50, 50);
            StringDriver.BasicGetRange(stringArr1, 0, 50);
            StringDriver.BasicGetRange(stringArr1, 50, 25);
            StringDriver.BasicGetRange(stringArr1, 0, 25);
            StringDriver.BasicGetRange(stringArr1, 75, 25);
            StringDriver.BasicGetRange(stringArr1, 0, 100);
            StringDriver.BasicGetRange(stringArr1, 0, 99);
            StringDriver.BasicGetRange(stringArr1, 1, 1);
            StringDriver.BasicGetRange(stringArr1, 99, 1);
        }

        [Fact]
        public static void SlicingWorks()
        {
            Driver<int> IntDriver = new Driver<int>();
            int[] intArr1 = new int[100];
            for (int i = 0; i < 100; i++)
                intArr1[i] = i;

            IntDriver.BasicSliceSyntax(intArr1, 50, 50);
            IntDriver.BasicSliceSyntax(intArr1, 0, 50);
            IntDriver.BasicSliceSyntax(intArr1, 50, 25);
            IntDriver.BasicSliceSyntax(intArr1, 0, 25);
            IntDriver.BasicSliceSyntax(intArr1, 75, 25);
            IntDriver.BasicSliceSyntax(intArr1, 0, 100);
            IntDriver.BasicSliceSyntax(intArr1, 0, 99);
            IntDriver.BasicSliceSyntax(intArr1, 1, 1);
            IntDriver.BasicSliceSyntax(intArr1, 99, 1);
        }

        [Fact]
        public static void GetRangeTests_Negative()
        {
            Driver<int> IntDriver = new Driver<int>();
            int[] intArr1 = new int[100];
            for (int i = 0; i < 100; i++)
                intArr1[i] = i;

            Driver<string> StringDriver = new Driver<string>();
            string[] stringArr1 = new string[100];
            for (int i = 0; i < 100; i++)
                stringArr1[i] = "SomeTestString" + i.ToString();

            StringDriver.GetRangeValidations(stringArr1);
            IntDriver.GetRangeValidations(intArr1);
        }

        [Fact]
        public static void ContainsTests()
        {
            Driver<int> IntDriver = new Driver<int>();
            int[] intArr1 = new int[10];
            for (int i = 0; i < 10; i++)
            {
                intArr1[i] = i;
            }

            int[] intArr2 = new int[10];
            for (int i = 0; i < 10; i++)
            {
                intArr2[i] = i + 10;
            }

            IntDriver.BasicContains(intArr1);
            IntDriver.NonExistingValues(intArr1, intArr2);


            Driver<string> StringDriver = new Driver<string>();
            string[] stringArr1 = new string[10];
            for (int i = 0; i < 10; i++)
            {
                stringArr1[i] = "SomeTestString" + i.ToString();
            }
            string[] stringArr2 = new string[10];
            for (int i = 0; i < 10; i++)
            {
                stringArr2[i] = "SomeTestString" + (i + 10).ToString();
            }

            StringDriver.BasicContains(stringArr1);
            StringDriver.NonExistingValues(stringArr1, stringArr2);
            StringDriver.ContainsNullWhenReference(stringArr1, null);
        }
    }
}
