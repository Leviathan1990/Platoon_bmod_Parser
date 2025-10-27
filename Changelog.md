Plat00n .bmod parser changelog.
=
Actual version: V 1.1.

[*Software improvements*]:
+  Full support for each .bmod files!

[*Additions*]:
+	Added custom location exporter function! Now you can extract the .obj, .tga and .mtl file to custom location!
+	Added .dds to .tga converter!

[*Fixes*]:
*	Fixed obj exporter function. Now .tga, .obj and .mtl files will be landed in the folder you create!
*	Fixed AnimationExporter.
*	Fixed BmodStructure.
*	Fixed MatFileReader.

***Knowing issues***
=
This version of my tool has undergone many fixes, and several critical issues have been resolved. However, there are still some problems with certain types of .bmod files.
My parser can now read and generate the .obj, .mtl, and .tga files for all the .bmod files stored in the following folders:

+  Gfx\Objects\Buildings\
+  Gfx\Objects\Vehicles\
+  Gfx\Objects\Plants
+  Gfx\Objects\Weapons

We only have problem with the contents of the following folders:

+  Gfx\Objects\Additional
+  Gfx\Objects\Humans

News: I finally managed to make some minor fixes, so it looks like I’ll be able to fix everything soon!

Now, let’s talk a bit about the animation files (stored in the *Gfx\Movement* folder). The game uses .bmod files to store animations as well, which is nice since everything can be packed into a single archive type. 
However, these animation .bmod files are different from the ones that only contain the geometry we need for the .obj files.

***Previous editions***
=

V 1.0 Initial Edition

[Additions]:

+  Created bmod parser project
+  Program now support some .bmod files!
