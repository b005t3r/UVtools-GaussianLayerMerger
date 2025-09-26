# UVtools-GaussianLayerMerger
A script for UVtools for stepping/print lines reduction by merging multiple thin layers (e.g. 10um) into a single thicker layer (e.g. 40um).

## What is it?
It's a UVtools script for processing slice files used for resing 3D printing. It lets you slice at a much lower layer height (normally unprintable or very difficult to print) and merge such slices into a single, thicker layer, while preserving detail fo the thinner layers, thus reducing common printing artefacts like the print lines or stepping.

## How does it work?
It works very similarly to the standard XY anti-aliasing, but here in the Z-axis - thinner layers are stacked one onto another and merged into a single thicker layer, while preserving the details of the thin layers, which creates a gradient that fils in the gaps between thicker layers which makes them much smoother.

## How do I use it?
1. Produce the slices as you would normally do (don't change your settings you normally use for your target layer height) BUT slice at a lower layer height (e.g., if you normally print at 40um, keep your settings for the 40um layers in your slicer and ONLY change the layer height to, say, 10um).
2. Open your output file in UVtools.
3. Optional, but makes things much faster for smaller prints - set ROI to model volume:
<img width="596" height="478" alt="image" src="https://github.com/user-attachments/assets/3a774838-4574-4351-a642-bb8c3b6789de" />
4. In the main menu select Tools -> Scripting and load the ScriptGaussianLayerMerger.cs file:
<img width="320" height="168" alt="image" src="https://github.com/user-attachments/assets/4d7d4869-cc98-4f5b-b138-0f89d8fb9e6f" />
5. Set up your parameters. This might require some experiments, since every printer is different. I got the best results with Anycubic Mono 4 Ultra with:
- 4 sub-layers (10um each)
- sampling depth of 3.5 (so it samples through three and a half 40um layers when creating a single 40um layer; I would normally keep this at sub-layer count minus 0.5)
- dimming of 70%
Those are the also the default settings set in the script.
6. When the processing finishes, save your file and use it for printing. Your layer exposure, wait times, number of bottoms layers, etc. will not be changed.

## Example results
...
