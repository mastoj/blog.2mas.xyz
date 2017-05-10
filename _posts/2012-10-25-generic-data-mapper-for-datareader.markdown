---
layout: post
title: Generic data mapper for DataReader
date: '2012-10-25 07:24:00'
tags:
- net
- ado-net
---

Isn't it boring to write mappers to map data from a db to objects? Wouldn't it be nice if someone wrote one that is simple to use and works in most cases? I guess so and maybe there are several out there, but I wrote one myself anyway. I know a lot of people think you should use EF, linq to sql, nHibernate or some other ORM... but this is an existing project and I want to improve that as much as I could with the tools at hand without adding new frameworks, I'm just glad we are moving away from `DataSet`s :).

This is sort of an update version of my earlier post [Generic mapper for datareader](http://blog.tomasjansson.com/2010/10/generic-mapper-for-datareader/), but I figured I could do it better.

First I want to say that the code is not optimized so if you have a really high volume of data access it might be slow, but for most situations this will most likely do just just fine. The implementation focus only on querying where you get a list of objects out from the query. I will be doing this top down so everything hopefully makes sense why I'm doing them.

Step 1: I wan't to query a database, and I want to do it in the same way regarding to error handling and logging. So I decided on some query code that look something like this

	public class MyDataAccessClass : BaseDataAccess, IMyDataAccessClass
	{
		public IList<MyEntity> GetEntities(DateTime date)
		{
			var database = new DataBase(); // 1.
			var dbCommand = database.GetStoredProcCommand("StoredProcName"); // 2.
			database.AddInParameter(dbCommand, "FromDate", DbType.DateTime, date); // 2.
			var result = ExecuteDbCommand<MyEntity>(db, dbCommand, "Property1"); // 3.
			return result;
		}
	}

So what do we got here?

1. We create our database object, could be any one just as the concept is the same. 
2. We create our command, this is the reason this code is not abstracted since the command we want to execute depends on the result we want and we need the database object to create the command. Also, add parameters to command.
3. We got something that will execute the query for us and return a list of objects of the type we specifies. The last parameter ("Property1") is used to define properties that I don't want to map when doing the query.

Step 2 will be to write the `ExecuteDbCommand<ReturnType>`. So to do that I implemented an abstract base class that all the data access classes can inherit from. It looks something like this:

	public abstract class DataAccessBase
	{
		protected IList<T> ExecuteDbCommand<T>(Database db, DbCommand command, params string[] excludeProperties) where T : new()
		{
	    	using (Tracer myTracer = new Tracer(Constants.TraceLog))
	    	{
				try
				{
	        		var reader = db.ExecuteReader(command); // 1.
	        		var mapper = new DataReaderMapper<T>(); // 2.
					var result = mapper.MapListExcludeColumns(reader, excludeProperties); // 3. 
	        		return result;
				}
				catch(Exception ex)
				{
					// Handle your exception
				}
	    	}
		}
	}
	
Ok, now it starts to get interesting. As you can see this takes any type `T` that has a default constructor (by constraint). So what does the code actually do, let me break it down for you:

1. Create the list where we want to store the objects we read from the `reader`.
2. Execute the databse command.
3. Call our mapper to map the reader to a list of objects.

No we are almost done, what we still have left is to implement our magic `DataReaderMapper` which will be our third and final step, it looks something like this:

	public class DataReaderMapper<T> where T : new()
	{
	    public IList<T> MapListAll(IDataReader reader)
	    {
	        return MapListExcludeColumns(reader);
	    }

	    public IList<T> MapListExcludeColumns(IDataReader reader, params string[] excludeColumns)
	    {
	        var listOfObjects = new List<T>();
	        while (reader.Read())
	        {
	            listOfObjects.Add(MapRowExclude(reader, excludeColumns));
	        }
	        return listOfObjects;
	    }

	    public T MapRowExclude(IDataReader reader, params string[] columns)
	    {
	        return MapRow(reader, false, columns);
	    }

	    public T MapRowInclude(IDataReader reader, params string[] columns)
	    {
	        return MapRow(reader, true, columns);
	    }

	    public T MapRowAll(IDataReader reader)
	    {
	        return MapRow(reader, true, null);
	    }

	    private T MapRow(IDataReader reader, bool includeColumns, params string[] columns)
	    {
	        T item = new T(); // 1. 
	        var properties = GetPropertiesToMap(includeColumns, columns); // 2. 
	        foreach (var property in properties)
	        {
				int ordinal = reader.GetOrdinal(property.Name); // 3. 
				if(!reader.IsDBNull(ordinal)) // 4.
				{
					// if dbnull the property will get default value, 
					// otherwise try to read the value from reader
	            	property.SetValue(item, reader[ordinal], null); // 5.
				}
	        }
	        return item;
	    }

	    public IEnumerable<System.Reflection.PropertyInfo> GetPropertiesToMap(bool includeColumns, string[] columns)
	    {

	        var properties = typeof(T).GetProperties().Where(y => 
	            (y.PropertyType.Equals(typeof(string)) || 
	            y.PropertyType.Equals(typeof(byte[])) ||
	            y.PropertyType.IsValueType) && 
	            (columns == null || (columns.Contains(y.Name) == includeColumns)));
	        return properties;
	    }
	}

I have removed all the comments to shorten the post :). As you can see there are a bunch of overloads and variations, like including/excluding properties etc. If we start with the `MapListExcludeColumns`, which is the reason we ended up here. The basic thoughts are to create a list and an item for each read of the reader. When we add an item we call the `MapRowExclude` method, which calls the private method `MapRow`... and that is where all the magic is and of course I will walk you through it:

1. Create the item that we will return.
2. Get the properties that we are going to map, if you look at the method it calls the method only returns value type properties, strings and byte arrays. That is because I want to keep it simple and assumes we have simple data objects. Also, it only returns those properties we want to have included.
3. Get the ordinal from the reader by the property name.
4. We only want to map if the reader actually has a value for that ordinal.
5. If it is not null set value using the `PropertyInfo` we have at this point.

**Note that as of now it works only by the convention that the column names in the reader is the same as of the properties in the target object**. That is something I will fix by adding a mapping concept using a simple dictionary, but that is something I will do later on.

I think this it, that's very easy to use mapper. As of now I think I am good. I will make a real project of this and it up on github when I get a hang of github :). If you find any errors or have any improvements suggestions please tell. 