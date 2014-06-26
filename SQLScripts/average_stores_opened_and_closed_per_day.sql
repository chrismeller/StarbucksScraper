declare @FirstSeen DateTime
declare @LastSeen DateTime
-- we had a lot of stores that first time we ran (obviously), so get the first date... and then the one right after it
set @FirstSeen = ( select min(FirstSeen) from Stores )
set @FirstSeen = ( select min(FirstSeen) from Stores where FirstSeen > @FirstSeen )

-- and for our end bound, we need the most recent date we ran
set @LastSeen = ( select max(LastSeen) from Stores )

--select FirstSeen, count(*) from Stores where FirstSeen >= @FirstSeen group by FirstSeen order by FirstSeen asc;

-- now get the number of stores added in that period
declare @StoresAdded int
set @StoresAdded = ( select count(*) from Stores where FirstSeen >= @FirstSeen )

-- and the number of stores deleted?
declare @StoresDeleted int
set @StoresDeleted = ( select count(*) from Stores where LastSeen < @LastSeen )

-- how many days are we talking about?
declare @Days int
set @Days = ( select DATEDIFF( day, @FirstSeen, @LastSeen ) )

select
	@Days as Days, 
	@StoresAdded as StoresAdded, 
	@StoresDeleted as StoresDeleted, 
	ROUND( CAST( @StoresAdded as float ) / @Days, 2 ) as AvgStoresAddedPerDay,
	ROUND( CAST( @StoresDeleted as float ) / @Days, 2 ) as AvgStoresDeletedPerDay;