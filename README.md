# AutoLipsSync

Unity editor and runtime tools for animating a VRM 1.0 avatar from a recording. The editor generates a timeline of mouth shapes and durations from an audio clip. The runtime blends VRM expression weights with attack, sustain, release and crossfade.

## Setup

1. Open the repository with the Unity version recorded in ProjectSettings/ProjectVersion.txt.
2. Let Unity resolve the UniVRM packages. Install [DOTween](https://dotween.demigiant.com/download.php) and run its Setup panel.
3. Import your own VRM 1.0 avatar and audio clip. Add AutoLipsSync and assign the avatar and its vowel expressions.
4. Open **Window → Phoneme Timeline Generator**, choose the clip and click **Generate Timeline**. For manual edits, select the timeline asset and use **Edit Timeline** in the Inspector.
5. Assign the timeline. The LipsSyncTest component can start the audio and lip animation together in a demo scene with an AudioSource and MoodController.

The analysis modes are FFT, LPC, hybrid and spectral centroid. The editor labels the last mode MFCC.


## License

[PolyForm Noncommercial License 1.0.0](LICENSE.md).
UniVRM and DOTween are separate dependencies under their own licenses.
