Baroutrama mod to simplify game UI based on [LuaCS](https://github.com/evilfactory/LuaCsForBarotrauma). Written as csharp in-memory mod which will be precompiled before load.
May be partially ported to Lua, but getter/setter can be only (currently) modified from csharp.

List of modified HUD elements:
1) Inventory now in lower left part of the screen;
2) Chatbox moved to the right side;
3) Disabled HUD elements: Crew commands, player HP (its now below character in-game), player avatar;
4) Chat messages becomes more compact in single line (if possible by word wrapping) P.S. *may cause conflicts if other mods overrides some chat functionality*;
5) Fixes left/right side layout for inventory, when Character inventory xml was changed to have lower than 7 slots (f.e. only 5 active slots)

Well tested on Win10/11 barotrauma 1.8.7.0.
