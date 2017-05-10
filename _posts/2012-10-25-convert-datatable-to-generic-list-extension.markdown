---
layout: post
title: Convert DataTable to generic list extension
date: '2012-10-25 07:13:00'
tags:
- net
---

The background to the following post is this [question][1] on Stack Overflow and my old [blog post][2] about generic list to DataTable. The question on Stack Overflow is basically asking the opposite of what I wrote in my blog post, but doing the opposite is almost the same code so I figured I just write it down and here it is:

    public static class DataTableExtensions
    {
        public static IList<T> ToList<T>(this DataTable table) where T : new()
        {
            IList<PropertyInfo> properties = typeof(T).GetProperties().ToList();
            IList<T> result = new List<T>();

            foreach (var row in table.Rows)
            {
                var item = CreateItemFromRow<T>((DataRow)row, properties);
                result.Add(item);
            }

            return result;
        }

        private static T CreateItemFromRow<T>(DataRow row, IList<PropertyInfo> properties) where T : new()
        {
            T item = new T();
            foreach (var property in properties)
            {
                property.SetValue(item, row[property.Name], null);
            }
            return item;
        }
    }

You could probably add some overload to exclude properties, I would in that case go for the signature `static List<T> ToList<T>(this DataTable table, params string[] excludeProperties)`, but I leave that up to you. You might also need some error handling depending on where the code is used.

  [1]: http://stackoverflow.com/questions/4104464/convert-datatable-to-generic-list-in-c
  [2]: http://blog.tomasjansson.com/2010/10/generic-list-to-datatable/