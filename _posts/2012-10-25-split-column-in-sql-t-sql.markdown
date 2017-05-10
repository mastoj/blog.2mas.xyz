---
layout: post
title: Split column in sql (t-sql)
date: '2012-10-25 07:25:00'
tags:
- sql
---

Ok, first of all you should never be in a situation where you need to split columns in a database if you ask me, that is, if you have done your job well. A sql database consists of tables and tables consists of columns so why join columns together and separate the values with ';'? Enough about that. The problem I got served was that there was such a column, comma separated, in the database and now I was asked to split that column into multiple columns, so how do you do that? There is actually two things you need to do:

1. Split your existing column
2. Insert the new data in the table
<p></p>
To split the column I used the following code that I found on [stackoverflow] [1]:

    CREATE FUNCTION dbo.Split (@sep char(1), @s varchar(512))
    RETURNS table
    AS
    RETURN (
        WITH Pieces(pn, start, stop) AS (
          SELECT 1, 1, CHARINDEX(@sep, @s)
          UNION ALL
          SELECT pn + 1, stop + 1, CHARINDEX(@sep, @s, stop + 1)
          FROM Pieces
          WHERE stop > 0
        )
    SELECT pn,
    SUBSTRING(@s, start, CASE WHEN stop > 0 THEN stop-start ELSE 512 END) AS s
    FROM Pieces
    )

The function takes the string you want to split and the character you want to split on and returns a table, for example would the following input string: "value1;value2" be:
<table>
   <tr>
      <td>1</td>
      <td>value1</td>
   </tr>
   <tr>
      <td>2</td>
      <td>value2</td>
   </tr>
</table>

The next step is to get these values back in the table and to do that you need to do the following things:

1. Create a cursor that loops over the ids in your table
2. For each id do a split, using the previous function, on the column that should be split and insert in a **temporary table** --> a lot of null values in the table (it's ok)
3. Group the temporary table and take the maximum value for each new column (the null values will disappear)
4. Update the original table with the new values.

In code it looks like this:

	-- Create temporary table for holding the new columns together with the respective id
	CREATE TABLE #NewColumnsTable 
	(
	         TableId int,
	         NewColumn1 varchar(2),
	         NewColumn2 varchar(7)
	);

	-- Temporary variable used in loop
	DECLARE @TableId int;

	-- Create cursor over all the ids in the table and open cursor
	DECLARE tableIdCursor CURSOR FOR
	SELECT TableId FROM YourTable;
	open tableIdCursor;

	-- Initial fetch
	fetch next from tableIdCursor into @TableId;

	-- Loop while we get a result from fetch
	While @@FETCH_STATUS = 0
	BEGIN
	         -- Update temporary table with splitted values
	         insert into #NewColumnsTable
	         SELECT @TableId as TableId
	         , CASE epb.pn
	                 when 1 then epb.s
	                 else null
	           end AS NewColumn1
	         , CASE epb.pn
	                 when 2 then epb.s
	                 else null
	           end AS NewColumn2
	         FROM  pgsa.split(';', 
	                          (SELECT Column2Split FROM YourTable yt2 Where yt2.TableId = @TableId)) epb;

	         -- Fetch the new besvarelseid
	         fetch next from tableIdCursor into @TableId;
	END

	-- Close and deallocate cursor since it is no longer in use
	close tableIdCursor;
	deallocate tableIdCursor;
	GO

	-- Group table to get one row of values per id
	SELECT * INTO #GroupedNewColumnsTable
	FROM (SELECT TableId
	         , MAX(NewColumn1) as NewColumn1
	         , MAX(NewColumn2) as NewColumn2
	FROM #NewColumnsTable
	GROUP BY TableId) as t;
	GO

	-- Update table with the values for the new columns
	UPDATE YourTable
	SET NewColumn1 = bu.NewColumn1,
	         NewColumn2 = bu.NewColumn2
	FROM YourTable b
	INNER JOIN #GroupedNewColumnsTable bu
	ON b.TableId = bu.TableId;
	GO

[1]: http://stackoverflow.com/questions/314824/t-sql-opposite-to-string-concatenation-how-to-split-string-into-multiple-recor