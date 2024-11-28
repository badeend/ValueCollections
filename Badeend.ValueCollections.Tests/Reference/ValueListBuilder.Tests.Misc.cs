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
    public class ValueListBuilder_Tests_Insert
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

            #region Insert

            public void BasicInsert(T[] items, T item, int index, int repeat)
            {
                ValueList<T>.Builder list = ValueList.CreateBuilder<T>(items);

                for (int i = 0; i < repeat; i++)
                {
                    list.Insert(index, item);
                }

                Assert.True(list.Contains(item)); //"Expect it to contain the item."
                Assert.Equal(list.Count, items.Length + repeat); //"Expect to be the same."


                for (int i = 0; i < index; i++)
                {
                    Assert.Equal(list[i], items[i]); //"Expect to be the same."
                }

                for (int i = index; i < index + repeat; i++)
                {
                    Assert.Equal(list[i], item); //"Expect to be the same."
                }


                for (int i = index + repeat; i < list.Count; i++)
                {
                    Assert.Equal(list[i], items[i - repeat]); //"Expect to be the same."
                }
            }

            public void InsertValidations(T[] items)
            {
                ValueList<T>.Builder list = ValueList.CreateBuilder<T>(items);
                int[] bad = new int[] { items.Length + 1, items.Length + 2, int.MaxValue, -1, -2, int.MinValue };
                for (int i = 0; i < bad.Length; i++)
                {
                    Assert.Throws<ArgumentOutOfRangeException>(() => list.Insert(bad[i], items[0])); //"ArgumentOutOfRangeException expected."
                }
            }


            #endregion

            #region InsertRange

            public void InsertRangeIEnumerable(T[] itemsX, T[] itemsY, int index, int repeat, Func<T[], IEnumerable<T>> constructIEnumerable)
            {
                ValueList<T>.Builder list = constructIEnumerable(itemsX).ToValueListBuilder();

                for (int i = 0; i < repeat; i++)
                {
                    list.InsertRange(index, constructIEnumerable(itemsY));
                }

                foreach (T item in itemsY)
                {
                    Assert.True(list.Contains(item)); //"Should contain the item."
                }
                Assert.Equal(list.Count, itemsX.Length + (itemsY.Length * repeat)); //"Should have the same result."

                for (int i = 0; i < index; i++)
                {
                    Assert.Equal(list[i], itemsX[i]); //"Should have the same result."
                }

                for (int i = index; i < index + (itemsY.Length * repeat); i++)
                {
                    Assert.Equal(list[i], itemsY[(i - index) % itemsY.Length]); //"Should have the same result."
                }

                for (int i = index + (itemsY.Length * repeat); i < list.Count; i++)
                {
                    Assert.Equal(list[i], itemsX[i - (itemsY.Length * repeat)]); //"Should have the same result."
                }

                //InsertRange into itself
                list = constructIEnumerable(itemsX).ToValueListBuilder();
                list.InsertRange(index, list.AsCollection().ToValueList());

                foreach (T item in itemsX)
                {
                    Assert.True(list.Contains(item)); //"Should contain the item."
                }
                Assert.Equal(list.Count, itemsX.Length + (itemsX.Length)); //"Should have the same result."

                for (int i = 0; i < index; i++)
                {
                    Assert.Equal(list[i], itemsX[i]); //"Should have the same result."
                }

                for (int i = index; i < index + (itemsX.Length); i++)
                {
                    Assert.Equal(list[i], itemsX[(i - index) % itemsX.Length]); //"Should have the same result."
                }

                for (int i = index + (itemsX.Length); i < list.Count; i++)
                {
                    Assert.Equal(list[i], itemsX[i - (itemsX.Length)]); //"Should have the same result."
                }
            }

            public void InsertRangeValidations(T[] items, Func<T[], IEnumerable<T>> constructIEnumerable)
            {
                ValueList<T>.Builder list = constructIEnumerable(items).ToValueListBuilder();
                int[] bad = new int[] { items.Length + 1, items.Length + 2, int.MaxValue, -1, -2, int.MinValue };
                for (int i = 0; i < bad.Length; i++)
                {
                    Assert.Throws<ArgumentOutOfRangeException>(() => list.InsertRange(bad[i], constructIEnumerable(items))); //"ArgumentOutOfRangeException expected"
                }

                Assert.Throws<ArgumentNullException>(() => list.InsertRange(0, (IEnumerable<T>)null!)); //"ArgumentNullException expected."
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
                return items.ToValueListBuilder().AsCollection();
            }

            #endregion

            #region Contains

            public void BasicContains(T[] items)
            {
                ValueList<T>.Builder list = ValueList.CreateBuilder<T>(items);

                for (int i = 0; i < items.Length; i++)
                {
                    Assert.True(list.Contains(items[i])); //"Should contain item."
                }
            }

            public void NonExistingValues(T[] itemsX, T[] itemsY)
            {
                ValueList<T>.Builder list = ValueList.CreateBuilder<T>(itemsX);

                for (int i = 0; i < itemsY.Length; i++)
                {
                    Assert.False(list.Contains(itemsY[i])); //"Should not contain item"
                }
            }

            public void RemovedValues(T[] items)
            {
                ValueList<T>.Builder list = ValueList.CreateBuilder<T>(items);
                for (int i = 0; i < items.Length; i++)
                {
                    list.Remove(items[i]);
                    Assert.False(list.Contains(items[i])); //"Should not contain item"
                }
            }

            public void AddRemoveValues(T[] items)
            {
                ValueList<T>.Builder list = ValueList.CreateBuilder<T>(items);
                for (int i = 0; i < items.Length; i++)
                {
                    list.Add(items[i]);
                    list.Remove(items[i]);
                    list.Add(items[i]);
                    Assert.True(list.Contains(items[i])); //"Should contain item."
                }
            }

            public void MultipleValues(T[] items, int times)
            {
                ValueList<T>.Builder list = ValueList.CreateBuilder<T>(items);

                for (int i = 0; i < times; i++)
                {
                    list.Add(items[items.Length / 2]);
                }

                for (int i = 0; i < times + 1; i++)
                {
                    Assert.True(list.Contains(items[items.Length / 2])); //"Should contain item."
                    list.Remove(items[items.Length / 2]);
                }
                Assert.False(list.Contains(items[items.Length / 2])); //"Should not contain item"
            }
            public void ContainsNullWhenReference(T[] items, T value)
            {
                if ((object)value != null)
                {
                    throw new ArgumentException("invalid argument passed to testcase");
                }

                ValueList<T>.Builder list = ValueList.CreateBuilder<T>(items);
                list.Add(value);
                Assert.True(list.Contains(value)); //"Should contain item."
            }

            #endregion

            #region Clear

            public void ClearEmptyList()
            {
                ValueList<T>.Builder list = ValueList.CreateBuilder<T>();
                Assert.Equal(0, list.Count); //"Should be equal to 0"
                list.Clear();
                Assert.Equal(0, list.Count); //"Should be equal to 0."
            }
            public void ClearMultipleTimesEmptyList(int times)
            {
                ValueList<T>.Builder list = ValueList.CreateBuilder<T>();
                Assert.Equal(0, list.Count); //"Should be equal to 0."
                for (int i = 0; i < times; i++)
                {
                    list.Clear();
                    Assert.Equal(0, list.Count); //"Should be equal to 0."
                }
            }
            public void ClearNonEmptyList(T[] items)
            {
                ValueList<T>.Builder list = ValueList.CreateBuilder<T>(items);
                list.Clear();
                Assert.Equal(0, list.Count); //"Should be equal to 0."
            }

            public void ClearMultipleTimesNonEmptyList(T[] items, int times)
            {
                ValueList<T>.Builder list = ValueList.CreateBuilder<T>(items);
                for (int i = 0; i < times; i++)
                {
                    list.Clear();
                    Assert.Equal(0, list.Count); //"Should be equal to 0."
                }
            }

            #endregion
        }

        [Fact]
        public static void InsertTests()
        {
            Driver<int> IntDriver = new Driver<int>();
            int[] intArr1 = new int[100];
            for (int i = 0; i < 100; i++)
                intArr1[i] = i;

            int[] intArr2 = new int[100];
            for (int i = 0; i < 100; i++)
                intArr2[i] = i + 100;

            IntDriver.BasicInsert(new int[0], 1, 0, 3);
            IntDriver.BasicInsert(intArr1, 101, 50, 4);
            IntDriver.BasicInsert(intArr1, 100, 100, 5);
            IntDriver.BasicInsert(intArr1, 100, 99, 6);
            IntDriver.BasicInsert(intArr1, 50, 0, 7);
            IntDriver.BasicInsert(intArr1, 50, 1, 8);
            IntDriver.BasicInsert(intArr1, 100, 50, 50);

            Driver<string> StringDriver = new Driver<string>();
            string[] stringArr1 = new string[100];
            for (int i = 0; i < 100; i++)
                stringArr1[i] = "SomeTestString" + i.ToString();
            string[] stringArr2 = new string[100];
            for (int i = 0; i < 100; i++)
                stringArr2[i] = "SomeTestString" + (i + 100).ToString();

            StringDriver.BasicInsert(stringArr1, "strobia", 99, 2);
            StringDriver.BasicInsert(stringArr1, "strobia", 100, 3);
            StringDriver.BasicInsert(stringArr1, "strobia", 0, 4);
            StringDriver.BasicInsert(stringArr1, "strobia", 1, 5);
            StringDriver.BasicInsert(stringArr1, "strobia", 50, 51);
            StringDriver.BasicInsert(stringArr1, "strobia", 0, 100);
            StringDriver.BasicInsert(new string[] { null, null, null, "strobia", null }, null, 2, 3);
            StringDriver.BasicInsert(new string[] { null, null, null, null, null }, "strobia", 0, 5);
            StringDriver.BasicInsert(new string[] { null, null, null, null, null }, "strobia", 5, 1);
        }

        [Fact]
        public static void InsertTests_negative()
        {
            Driver<int> IntDriver = new Driver<int>();
            int[] intArr1 = new int[100];
            for (int i = 0; i < 100; i++)
                intArr1[i] = i;
            IntDriver.InsertValidations(intArr1);

            Driver<string> StringDriver = new Driver<string>();
            string[] stringArr1 = new string[100];
            for (int i = 0; i < 100; i++)
                stringArr1[i] = "SomeTestString" + i.ToString();
            StringDriver.InsertValidations(stringArr1);
        }

        [Fact]
        public static void InsertRangeTests()
        {
            Driver<int> IntDriver = new Driver<int>();
            int[] intArr1 = new int[100];
            for (int i = 0; i < 100; i++)
                intArr1[i] = i;

            int[] intArr2 = new int[10];
            for (int i = 0; i < 10; i++)
            {
                intArr2[i] = i + 100;
            }

            foreach (Func<int[], IEnumerable<int>> collectionGenerator in IntDriver.CollectionGenerators)
            {
                IntDriver.InsertRangeIEnumerable(new int[0], intArr1, 0, 1, collectionGenerator);
                IntDriver.InsertRangeIEnumerable(intArr1, intArr2, 0, 1, collectionGenerator);
                IntDriver.InsertRangeIEnumerable(intArr1, intArr2, 1, 1, collectionGenerator);
                IntDriver.InsertRangeIEnumerable(intArr1, intArr2, 99, 1, collectionGenerator);
                IntDriver.InsertRangeIEnumerable(intArr1, intArr2, 100, 1, collectionGenerator);
                IntDriver.InsertRangeIEnumerable(intArr1, intArr2, 50, 50, collectionGenerator);
            }

            Driver<string> StringDriver = new Driver<string>();
            string[] stringArr1 = new string[100];
            for (int i = 0; i < 100; i++)
                stringArr1[i] = "SomeTestString" + i.ToString();
            string[] stringArr2 = new string[10];
            for (int i = 0; i < 10; i++)
                stringArr2[i] = "SomeTestString" + (i + 100).ToString();

            foreach (Func<string[], IEnumerable<string>> collectionGenerator in StringDriver.CollectionGenerators)
            {
                StringDriver.InsertRangeIEnumerable(new string[0], stringArr1, 0, 1, collectionGenerator);
                StringDriver.InsertRangeIEnumerable(stringArr1, stringArr2, 0, 1, collectionGenerator);
                StringDriver.InsertRangeIEnumerable(stringArr1, stringArr2, 1, 1, collectionGenerator);
                StringDriver.InsertRangeIEnumerable(stringArr1, stringArr2, 99, 1, collectionGenerator);
                StringDriver.InsertRangeIEnumerable(stringArr1, stringArr2, 100, 1, collectionGenerator);
                StringDriver.InsertRangeIEnumerable(stringArr1, stringArr2, 50, 50, collectionGenerator);
                StringDriver.InsertRangeIEnumerable(new string[] { null, null, null, null }, stringArr2, 0, 1, collectionGenerator);
                StringDriver.InsertRangeIEnumerable(new string[] { null, null, null, null }, stringArr2, 4, 1, collectionGenerator);
                StringDriver.InsertRangeIEnumerable(new string[] { null, null, null, null }, new string[] { null, null, null, null }, 0, 1, collectionGenerator);
                StringDriver.InsertRangeIEnumerable(new string[] { null, null, null, null }, new string[] { null, null, null, null }, 4, 50, collectionGenerator);
            }
        }

        [Fact]
        public static void InsertRangeTests_Negative()
        {
            Driver<int> IntDriver = new Driver<int>();
            int[] intArr1 = new int[100];
            for (int i = 0; i < 100; i++)
                intArr1[i] = i;
            Driver<string> StringDriver = new Driver<string>();
            string[] stringArr1 = new string[100];
            for (int i = 0; i < 100; i++)
                stringArr1[i] = "SomeTestString" + i.ToString();

            IntDriver.InsertRangeValidations(intArr1, IntDriver.ConstructTestEnumerable);
            StringDriver.InsertRangeValidations(stringArr1, StringDriver.ConstructTestEnumerable);
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
            IntDriver.RemovedValues(intArr1);
            IntDriver.AddRemoveValues(intArr1);
            IntDriver.MultipleValues(intArr1, 3);
            IntDriver.MultipleValues(intArr1, 5);
            IntDriver.MultipleValues(intArr1, 17);


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
            StringDriver.RemovedValues(stringArr1);
            StringDriver.AddRemoveValues(stringArr1);
            StringDriver.MultipleValues(stringArr1, 3);
            StringDriver.MultipleValues(stringArr1, 5);
            StringDriver.MultipleValues(stringArr1, 17);
            StringDriver.ContainsNullWhenReference(stringArr1, null);
        }

        [Fact]
        public static void ClearTests()
        {
            Driver<int> IntDriver = new Driver<int>();
            int[] intArr = new int[10];
            for (int i = 0; i < 10; i++)
            {
                intArr[i] = i;
            }

            IntDriver.ClearEmptyList();
            IntDriver.ClearMultipleTimesEmptyList(1);
            IntDriver.ClearMultipleTimesEmptyList(10);
            IntDriver.ClearMultipleTimesEmptyList(100);
            IntDriver.ClearNonEmptyList(intArr);
            IntDriver.ClearMultipleTimesNonEmptyList(intArr, 2);
            IntDriver.ClearMultipleTimesNonEmptyList(intArr, 7);
            IntDriver.ClearMultipleTimesNonEmptyList(intArr, 31);

            Driver<string> StringDriver = new Driver<string>();
            string[] stringArr = new string[10];
            for (int i = 0; i < 10; i++)
            {
                stringArr[i] = "SomeTestString" + i.ToString();
            }

            StringDriver.ClearEmptyList();
            StringDriver.ClearMultipleTimesEmptyList(1);
            StringDriver.ClearMultipleTimesEmptyList(10);
            StringDriver.ClearMultipleTimesEmptyList(100);
            StringDriver.ClearNonEmptyList(stringArr);
            StringDriver.ClearMultipleTimesNonEmptyList(stringArr, 2);
            StringDriver.ClearMultipleTimesNonEmptyList(stringArr, 7);
            StringDriver.ClearMultipleTimesNonEmptyList(stringArr, 31);
        }
    }
}
