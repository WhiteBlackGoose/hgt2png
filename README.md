# Hgt2png

Cross-platform tool for transforming hgt files into 32-bit and 64-bit pngs.

By <a href="https://github.com/WhiteBlackGoose">WhiteBlackGoose</a> and <a href="https://github.com/MomoDeve">MomoDeve</a>.

## Installation

```
git clone https://github.com/WhiteBlackGoose/hgt2png
cd hgt2png/hgt2png
dotnet build hgt2png.csproj
```

## Usage


Consider <a href="./samples/N44E033.hgt">this file</a>.


#### Automatic

For cases when you only need one fragment

```
dotnet run hgt2png.csproj "../samples/N44E033.hgt" "../samples/N44E033"
```

Auto-adjusted 32-bit example (the maximum height is considered to be 2^8-1, others are linearly adjusted)
<img src="./samples/N44E033_32bit_maxbyte_4.png">

maxbyte_4 means that when looking for the biggest height in the map, it found the height of at least 4 * 256. It means that
to put all heights in range [0..255] it divided every height by 4+1=5.


Pure 64-bit example
<img src="./samples/N44E033_64bit.png">

This one has no adjustments, it stores 16 bit for each channel, implying that it can store the raw information from the file.

#### Manual

If you need to process multiple hgt files, you need the same maxbyte value for each of them. You need to take the highest maxbyte of them.
Assume you have one file's maxbyte 3, another one's is 5, and the third one's is 4. You need "-maxbyte 5" for all of them.

```
dotnet run hgt2png.csproj "../samples/N44E033.hgt" "../samples/N44E033" -maxbyte 5
```

Manually-adjusted 32-bit example (the maximum height is considered to be 2^8-1, others are linearly adjusted)
<img src="./samples/N44E033_32bit_maxbyte_5.png">

At this time, we specified maxbyte 5, hence, it will divide every height by 5+1=6 to fit it in range [0..255].

<img src="./samples/N44E033_64bit_lightened_maxbyte_5.png">
This is a 64-bit lightened fragment. To get it, we multiply the heights from the source by (256 / (maxbyte+1)), so that the biggest height
was about 2^16 - 1.

#### Interpolation

Sometimes there are broken dots (whose first byte is 255 or 128). Hgt2png interpolates them with the average of all non-broken dots around. If all dots around are broken,
it sets the broken dot to 0.