StarbucksScraper
================

A very mean, very barebones, C# Console app that hammers the Starbucks Store Locator API to extract every store in the world.


About
-----

I created this as a proof of concept and used it to scrape the dataset you can now find on [Socrata](https://opendata.socrata.com/Business/All-Starbucks-Locations-in-the-World/xy4y-c4mk).

It's a quick and dirty C# console app that does just enough to get me the data I wanted. I'm sure my use of the Entity Framework and numerous other areas could be refined... I don't care.

There is also a partner app that then uploads the data to a Socrata dataset that you specify (in the App.config), since you can't seem to upload Unicode data in a CSV file.
