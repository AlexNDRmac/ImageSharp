// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization
{
    /// <summary>
    /// Gets the closest color to the supplied color based upon the Euclidean distance.
    /// TODO: Expose this somehow.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal readonly struct EuclideanPixelMap<TPixel> : IPixelMap<TPixel>, IEquatable<EuclideanPixelMap<TPixel>>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private readonly Vector4[] vectorCache;
        private readonly ConcurrentDictionary<TPixel, int> distanceCache;

        /// <summary>
        /// Initializes a new instance of the <see cref="EuclideanPixelMap{TPixel}"/> struct.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="palette">The color palette to map from.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public EuclideanPixelMap(Configuration configuration, ReadOnlyMemory<TPixel> palette)
        {
            Guard.MustBeGreaterThan(palette.Length, 0, nameof(palette));

            this.Palette = palette;
            ReadOnlySpan<TPixel> paletteSpan = this.Palette.Span;
            this.vectorCache = new Vector4[paletteSpan.Length];
            this.distanceCache = new ConcurrentDictionary<TPixel, int>();

            PixelOperations<TPixel>.Instance.ToVector4(configuration, paletteSpan, this.vectorCache);
        }

        /// <inheritdoc/>
        public ReadOnlyMemory<TPixel> Palette
        {
            [MethodImpl(InliningOptions.ShortMethod)]
            get;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
            => obj is EuclideanPixelMap<TPixel> map && this.Equals(map);

        /// <inheritdoc/>
        public bool Equals(EuclideanPixelMap<TPixel> other)
            => this.Palette.Equals(other.Palette);

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public int GetClosestColor(TPixel color, out TPixel match)
        {
            ReadOnlySpan<TPixel> paletteSpan = this.Palette.Span;

            // Check if the color is in the lookup table
            if (!this.distanceCache.TryGetValue(color, out int index))
            {
                return this.GetClosestColorSlow(color, paletteSpan, out match);
            }

            match = paletteSpan[index];
            return index;
        }

        /// <inheritdoc/>
        public override int GetHashCode() => this.vectorCache.GetHashCode();

        [MethodImpl(InliningOptions.ShortMethod)]
        private int GetClosestColorSlow(TPixel color, ReadOnlySpan<TPixel> palette, out TPixel match)
        {
            // Loop through the palette and find the nearest match.
            int index = 0;
            float leastDistance = float.MaxValue;
            Vector4 vector = color.ToScaledVector4();
            ref TPixel paletteRef = ref MemoryMarshal.GetReference(palette);
            ref Vector4 vectorCacheRef = ref MemoryMarshal.GetReference<Vector4>(this.vectorCache);

            for (int i = 0; i < palette.Length; i++)
            {
                Vector4 candidate = Unsafe.Add(ref vectorCacheRef, i);
                float distance = Vector4.DistanceSquared(vector, candidate);

                // If it's an exact match, exit the loop
                if (distance == 0)
                {
                    index = i;
                    break;
                }

                if (distance < leastDistance)
                {
                    // Less than... assign.
                    index = i;
                    leastDistance = distance;
                }
            }

            // Now I have the index, pop it into the cache for next time
            this.distanceCache[color] = index;
            match = Unsafe.Add(ref paletteRef, index);
            return index;
        }
    }
}
