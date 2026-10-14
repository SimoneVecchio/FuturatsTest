# FuturatsTest

## Inputs

Select/Move Unit - Left Click
Deselect Unit - Right Click
Camera Movement - WASD
Zoom In/Out - Scroll Wheel

## GridManager

This class is what generates the grid and owns the helpers that store the grid information like GridOccupancy (which unit is on which tile, their destinations and step reservations).
Initially i was unsure which approach to take on the grid tool so after watching a Warhammer 40,000 Battlesector video I decided to generate it with quads in a single mesh.
Clicking on Edit Tiles you can edit the single tiles of the mesh making them not walkable, changing the tile moevement cost, changing its material (which creates another mesh) or adding a prefab on top of it which could be any game element.

## Units

Units are built from data assets.
Clicking on the buttons of the unit editor tool it's possible to change the sub units prefabs individually, otherwise it takes the default model set in data asset.
Selecting a unit highlights which tiles it can reach.

## Pathfinding

Given the constraints I decided to use A* pathfinding which is the most common in this kind of games.
A unit's position on the grid is based on its pivot and the offsets are the tiles the unit occupies relative to the pivot. A position is only valid if every tile of the units fit on a grid position.
To enter a position the unit must pay the most expensive tile cost; diagonal steps cost the same as straight steps.
Units can't be sent to tiles another unit is already heading to, or occupying, and when moving a units reserves the tiles of the step so 2 units can never be on the same tiles.
If during a movement a unit "collides" with another moving unit, it waits first and then looks for a detour and stops after a few failed attempts.


## Tools
Since the test was also about scalability I put a lot of value in editor tools which make life much easier in the long run, so I made the Grid and Unit tools,
I used some help from AI to get them exactly how I wanted them, I hope that's not an issue.
