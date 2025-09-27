# UVtools-GaussianLayerMerger
A script for [UVtools](https://github.com/sn4k3/UVtools) that reduces stepping/print lines by merging multiple thin layers (e.g. 10um) into a single thicker layer (e.g. 40um).

## What is it?
It’s a [UVtools](https://github.com/sn4k3/UVtools) script for processing slice files used in resin 3D printing. It lets you slice at a much lower layer height (normally unprintable or very difficult to print) and merge those slices into a single, thicker layer, while preserving the detail of the thinner layers. This reduces common printing artifacts like print lines or stepping.

## How does it work?
It works very similarly to standard XY anti-aliasing, but here it’s applied along the Z-axis - thinner layers are stacked on top of each other and merged into a single thicker layer, while preserving the details of the thin layers. This creates a gradient that fills in the gaps between thicker layers, making them much smoother.

## How do I use it?
1. Produce the slices as you normally would (don’t change the settings you usually use for your target layer height) BUT slice at a lower layer height. (e.g., if you normally print at 40um, keep your slicer settings for 40um layers and ONLY change the layer height to, say, 10um).

2. Open your output file in [UVtools](https://github.com/sn4k3/UVtools).

3. Optional, but makes things much faster for smaller prints - set ROI to model volume:

<img width="596" height="478" alt="image" src="https://github.com/user-attachments/assets/3a774838-4574-4351-a642-bb8c3b6789de" />

4. In the main menu, select Tools -> Scripting and load the ScriptGaussianLayerMerger.cs file:
<img width="320" height="168" alt="image" src="https://github.com/user-attachments/assets/4d7d4869-cc98-4f5b-b138-0f89d8fb9e6f" />

5. Set up your parameters. This might require some experimenting, since every printer is different. I got the best results on an Anycubic Mono 4 Ultra with:
- 4 sub-layers (10um each)
- sampling depth of 3.5 (so it samples through three and a half 40um layers when creating a single 40um layer; I usually keep this at sub-layer count minus 0.5)
- dimming of 40%

  These are also the default settings in the script, so I suggest using them as your starting point.

6. When processing finishes, save your file and use it for printing. Your layer exposure, wait times, number of bottom layers, etc. will not be changed.

## Example results
[Visit the Wiki section for result comparison](https://github.com/b005t3r/UVtools-GaussianLayerMerger/wiki/Example-results)
