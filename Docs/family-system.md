Family system in Revit
======================

The family system in Revit has the following 5 levels:

  1. Categories
  2. (Sub-categories)
  3. Families
  4. Types
  5. Instances


Categories
----------

Categories are like abstract super classes or metaclasses in OOP. They are 
predefined, cannot be added, edited or removed. For example: Ceilings, Walls,
Doors, Windows, Roofs, Floors, etc.


(Sub-categories)
----------------

Sub-categories are an extra and mostly optional level within categories. You can
associate certain portions of a family to sub-categories to control their 
visibility and visual settings. For example the Door Category has Glass, Panel, 
Frame, Opening, (and many more) Sub-categories so you can turn of the visibility
of some of them in certain views. Let's say, you don't want to see the openings 
annotation of the doors in the Front view.


Families
--------

Families are sub classes, or in the metaclass example class instances of 
Categories. 

The easiest example would be an ***IKEA SKARSTA sit/stand Desk*** which derives 
from the ***Furnitures*** category. Families can be saved as family files and 
can be loaded into projects or other families (nested families). This is the 
level for 3rd party content developers because families are like templates. 
Families usually contain 3d and 2d elements and some aspects of them can be 
controlled through parameters and constraints. Families in Revit has a separate 
editor called ***Family Editor*** which is different from the default 
***Project Editor***. The Family Editor is for designing objects, the Project 
Editor is for designing complex buildings and structures.

Families can have two types of parameters: 

  1. Type parameters, and 
  2. Instance parameters. 

An easy analogy would be that type parameters are like static variables and 
instance parameters are like instance variables in OOP.


Types
-----

Types are not a good fit in the OOP analogy. They still aren't instances, hence 
the name, but they contain exact values in their type parameters that are 
defined in their parent families. It's not necessary a good idea of looking at 
them like as types in OOP. They more like a set of values for the type 
parameters. In real life they act more like variants of elements. For example, 
the family of ***IKEA SKARSTA sit/stand Desk*** would have the following types:

  1. SKARSTA Desk sit/stand, white 120x70 cm
  2. SKARSTA Desk sit/stand, white 160x80 cm

So they are products of the same product line.


Instances
---------

Instances are the instantiated types, just like instances in OOP. In this case, 
copies of the ***SKARSTA Desk sit/stand, white 120x70 cm*** type placed in an 
office building.

As mentioned before, families define type and instance parameters. A good 
example of an instance parameter (which acts just like an instance variable in 
OOP) would be the current height of the sit/stand desk in our previous IKEA 
example. That's because that this desk's height can be adjusted by the user and 
not defined by the manufacturer. Additionally, the min and max height can be 
defined in the family level by a dimension constrain or with parameter formulas.


