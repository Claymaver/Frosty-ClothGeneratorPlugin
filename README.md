# Cloth Generator Plugin for Frosty Editor

![FrostyEditor](https://img.shields.io/badge/Frosty%20Editor-1.0.6.3+-blue)
![Game](https://img.shields.io/badge/Game-Star%20Wars%20Battlefront%20II%20(2017)-orange)
![Status](https://img.shields.io/badge/Status-Alpha-yellow)

A lightweight Frosty Editor plugin that generates cloth data directly
inside the editor.


Built specifically for **Star Wars Battlefront II (2017)** modding.

------------------------------------------------------------------------

#  What This Plugin Does

If your custom mesh includes cloth (capes, kama, straps, waist cloth,
etc.), this plugin will:

-   Copy working cloth data from an existing mesh
-   Apply it to your custom mesh
-   Automatically create the required cloth resources
-   Save everything directly into your Frosty project

Simple workflow. No sliders. No manual tweaking.

------------------------------------------------------------------------

#  Installation

1.  Copy:

        ClothDataPlugin.dll

2.  Paste it into:

        Frosty Editor\Plugins\

That's it. Launch Frosty and you're ready to go.

------------------------------------------------------------------------

#  How To Use

## Step 1 --- Import Your Mesh

Import your FBX using: - Frosty's built-in FBX importer
- Or your custom mesh importer plugin (LINK)

------------------------------------------------------------------------

## Step 2 --- Generate Cloth Data

In **Data Explorer**:

-   Right-click your `SkinnedMeshAsset`
-   Click `Generate Cloth Data`

------------------------------------------------------------------------

## Step 3 --- Choose a Template Mesh (Important)

In the window that opens:

Under:

    Template Mesh (copy cloth from)

Click:

    Browse Mesh

Select a mesh that:

-   Already has working cloth
-   Is similar to your mesh
-   Uses the same skeleton

The plugin will copy:

-   ClothWrapping
-   EACloth

From that mesh to yours.

------------------------------------------------------------------------

## Step 4 --- Generate

Click:

    Generate

That's it.

Save your project and apply your mod.

# 🛠 Common Problems

## Cloth Doesn't Move

-   Template mesh must use the same skeleton
-   Your mesh must have correct bone weights

## Game Crashes

-   Mesh and cloth must be in the same bundle
-   Bone weights must match the skeleton
-   Try a different template mes

## "No existing cloth resources found"

This is normal for new meshes.

The plugin will create them automatically.

------------------------------------------------------------------------

#  Requirements

-   Frosty Editor 1.0.6.3+
-   Star Wars Battlefront II (2017) loaded in Frosty

------------------------------------------------------------------------

#  Credits

Original cloth format research from FrostMeshy.
Credit to Schnick for the original logic.
