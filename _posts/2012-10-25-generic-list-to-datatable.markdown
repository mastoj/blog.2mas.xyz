---
layout: post
title: Generic list to DataTable
date: '2012-10-25 07:20:00'
tags:
- net
- post
---

Ok, I have to admit that I'm sitting on a WebForms project that is using datasets and `DataTable` a lot as `DataSource` to `GridView`'s for example. When using `GridView`'s in asp.net it's pretty nice to have `DataTable` as `DataSource`s since the sorting of data is included in the `DataSource` object, as long as you don't needs paging than you have to take more things into consideration. However, we've decided to implement all the services to send list of objects instead of datasets, which I think is good, but we still want to use `DataTable`'s as `DataSource`. So how do we achieve this? Easy I say, all you need is some basic knowledge in generics and reflection to write an extension for `IList<T>` that creates a `DataTable` based on the generic type `T`. I ended up writing four methods; two public extension methods, one private extension method and one helper method:

    public static class ListExtensions
    {
        public static DataTable ToDataTable<T>(this IList<T> list)
        {
            IList<PropertyInfo> properties = list.GetPropertiesOfObjectInList();
            DataTable resultTable = CreateTable(properties);

            foreach(var item in list)
            {
                var row = CreateRowFromItem<T>(resultTable, item);
                resultTable.Rows.Add(row);
            }

            return resultTable;
        }

        private static DataTable CreateTable(IList<PropertyInfo> properties)
        {
            DataTable resultTable = new DataTable();
            foreach (var property in properties)
            {
                resultTable.Columns.Add(property.Name, property.PropertyType);
            }
            return resultTable;
        }

        public static IList<PropertyInfo> GetPropertiesOfObjectInList<T>(this IList<T> list)
        {
            return typeof(T).GetProperties().ToList();
        }

        private static DataRow CreateRowFromItem<T>(DataTable resultTable, T item)
        {
            var row = resultTable.NewRow();
            var properties = item.GetType().GetProperties().ToList();
            foreach (var property in properties)
            {
                row[property.Name] = property.GetValue(item, null);
            }
            return row;
        }
    }

What they do are the following:

* `ToDataTable` takes an `IList<T>` and creates the `DataTable` with data
* `CreateTable` creates the actual table from an `IList<T>`
* `GetPropertiesOfObjectInList` gets the properties of the object of type `T` (you need that to create the table)
* `CreateRowFromItem` takes the resulting table, so you can ask for the structure of a row, and the item you wish to add and creates a `DataRow` object of the item

That's how you spicy up your old WebForms project :). 