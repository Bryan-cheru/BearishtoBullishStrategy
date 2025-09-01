B2B Indicator
Version 1
5-Sept-2024



Definition
B2B is an acronym Bearish to bullish and vice versa. its for Close Above the High Of the Low Day. It refers to a possible turning point (aka pivot point) of the daily price trend of a stock or futures contract. Using (Japanese) candlestick chart patterns, a CAHOLD bar represents a reversal candle, signaling a change of trend from bearish to bullish.
CBLOHD is the opposite of CAHOLD. It stands for Close Below the Low Of the High Day. A CBLOHD candle is an indication of a change of trend from bullish to bearish.



Indicator Specification
The custom NinjaTrader 8 indicator that needs to be programmed in NinjaScript (C#) should recognize when a closed bar is either a CAHOLD candle, a CBLOHD bar, or neither, based on the value of the justclosed candle and the low or high of the relevant high or low bar in the recent trend.
The CAHOLD or CBLOHD candle color should be a different color than the normal green and red, such color to be designated by the user in Properties. The default color for such a reversal candle should be cyan for a CAHOLD and yellow for a CBLOHD.
In addition, an up arrow below the CAHOLD candle (default color green, default size 5) should appear below the colored candle. A down arrow above the CBLOHD bar (default color red, default size 5) should appear above the candle.
Finally, the visibility or not of the arrows should be designated by the user (default on), as well as the unique colors of the trend-change bars (default on).



Definition of Low Day & High Day
Using the trading concept of a swing high and swing low (see NinjaTrader’s Zig Zag indicator for reference), the default determination of the high or low day should include the wick (aka tail or shadow) of the candle. However, the user should be able to choose not to use the wick, but instead only the body of the candle.



Other Time Periods
Although this document refers to daily charts, the indicator should work on a 1-minute, 5-minute, 30minute (etc) NinjaTrader chart as well.



Other Project Requirements
The Properties Setup section must include as the 1st line the Version number of the program, followed by a 2nd line with the Date of that version. The Account field (or entire Setup section if necessary) should be near the top of Properties, not near the bottom. Label visible checkbox; Default unchecked. Order Handling section default: Exit on section close Yes (checked), seconds 3600. Execution Mode section: Include/Exclude Historical checkbox.



Testing
Do not submit new code unless parameter Templates have been tested and are working.



Program Submission
