﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Drawing;

namespace BizHawk.Bizware.BizwareGL
{
	public static class TexAtlas
	{
		public class RectItem
		{
			public RectItem(int width, int height, object item)
			{
				Width = width;
				Height = height;
				Item = item;
			}

			public int X, Y;
			public int Width, Height;
			public int TexIndex;
			public object Item;
		}

		private class TryFitParam
		{
			public TryFitParam(int _w, int _h) { this.w = _w; this.h = _h; }
			public readonly int w;
			public readonly int h;
			public bool ok = true;
			public readonly RectangleBinPack rbp = new RectangleBinPack();
			public readonly List<RectangleBinPack.Node> nodes = new List<RectangleBinPack.Node>();
		}

		public const int MaxSizeBits = 16;

		/// <summary>
		/// packs the supplied RectItems into an atlas. Modifies the RectItems with x/y values of location in new atlas.
		/// </summary>
		public static IReadOnlyList<(Size Size, List<RectItem> Items)> PackAtlas(IEnumerable<RectItem> items)
		{
			static void AddAtlas(ICollection<(Size, List<RectItem>)> atlases, IEnumerable<RectItem> initItems)
			{
				List<RectItem> currentItems = new(initItems);
				List<RectItem> remainItems = new();

				TryFitParam tfpFinal;
				while (true)
				{
					// this is where the texture size range is determined.
					// we run this every time we make an atlas, in case we want to variably control the maximum texture output size.
					// ALSO - we accumulate data in there, so we need to refresh it each time. ... lame.
					var todoSizes = new List<TryFitParam>();
					for (var i = 3; i <= MaxSizeBits; i++)
					{
						for (var j = 3; j <= MaxSizeBits; j++)
						{
							var w = 1 << i;
							var h = 1 << j;
							var tfp = new TryFitParam(w, h);
							todoSizes.Add(tfp);
						}
					}

					//run the packing algorithm on each potential size
					Parallel.ForEach(todoSizes, (param) =>
					{
						var rbp = new RectangleBinPack();
						rbp.Init(16384, 16384);
						param.rbp.Init(param.w, param.h);

						// ReSharper disable once AccessToModifiedClosure
						foreach (var ri in currentItems)
						{
							var node = param.rbp.Insert(ri.Width, ri.Height);
							if (node == null)
							{
								param.ok = false;
							}
							else
							{
								node.ri = ri;
								param.nodes.Add(node);
							}
						}
					});

					// find the best fit among the potential sizes that worked
					var best = long.MaxValue;
					tfpFinal = todoSizes[0];
					foreach (var tfp in todoSizes)
					{
						if (!tfp.ok)
						{
							continue;
						}

						var area = tfp.w * (long) tfp.h;
						if (area > best)
						{
							continue; // larger than best, not interested
						}

						if (area == best) // same area, compare perimeter as tie-breaker (to create squares, which are nicer to look at)
						{
							if (tfp.w + tfp.h >= tfpFinal.w + tfpFinal.h)
							{
								continue;
							}
						}

						best = area;
						tfpFinal = tfp;
					}

					//did we find any fit?
					if (best < long.MaxValue)
					{
						break;
					}

					//nope - move an item to the remaining list and try again
					remainItems.Add(currentItems[currentItems.Count - 1]);
					currentItems.RemoveAt(currentItems.Count - 1);
				}

				//we found a fit. setup this atlas in the result and drop the items into it
				atlases.Add((new(tfpFinal.w, tfpFinal.h), new(currentItems)));
				foreach (var item in currentItems)
				{
					var node = tfpFinal.nodes.Find(x => x.ri == item);
					item.X = node.x;
					item.Y = node.y;
					item.TexIndex = atlases.Count - 1;
				}

				//if we have any items left, we've got to run this again
				if (remainItems.Count > 0) AddAtlas(atlases, remainItems);
			}

			List<(Size, List<RectItem>)> atlases = new();
			AddAtlas(atlases, items);
			if (atlases.Count > 1) Console.WriteLine($"Created animset with >1 texture ({atlases.Count} textures)");
			return atlases;
		}

		// original file: RectangleBinPack.cpp
		// author: Jukka Jylänki
		private class RectangleBinPack
		{
			/** A node of a binary tree. Each node represents a rectangular area of the texture
				we surface. Internal nodes store rectangles of used data, whereas leaf nodes track
				rectangles of free space. All the rectangles stored in the tree are disjoint. */
			public class Node
			{
				// Left and right child. We don't really distinguish which is which, so these could
				// as well be child1 and child2.
				public Node left;
				public Node right;

				// The top-left coordinate of the rectangle.
				public int x;
				public int y;

				// The dimension of the rectangle.
				public int width;
				public int height;

				public RectItem ri;
			}

			/// <summary>Starts a new packing process to a bin of the given dimension.</summary>
			public void Init(int width, int height)
			{
				binWidth = width;
				binHeight = height;
				root = new();
				root.left = root.right = null;
				root.x = root.y = 0;
				root.width = width;
				root.height = height;
			}


			/// <summary>Inserts a new rectangle of the given size into the bin.</summary>
			/// <returns>A pointer to the node that stores the newly added rectangle, or 0 if it didn't fit.</returns>
			/// <remarks>Running time is linear to the number of rectangles that have been already packed.</remarks>
			public Node Insert(int width, int height)
			{
				return Insert(root, width, height);
			}

			/// <summary>Computes the ratio of used surface area.</summary>
			private float Occupancy()
			{
				var totalSurfaceArea = binWidth * binHeight;
				var usedSurfaceArea = UsedSurfaceArea(root);

				return (float)usedSurfaceArea / totalSurfaceArea;
			}

			private Node root;

			// The total size of the bin we started with.
			private int binWidth;
			private int binHeight;

			/// <returns>The surface area used by the subtree rooted at node.</returns>
			private static int UsedSurfaceArea(Node node)
			{
				if (node.left != null || node.right != null)
				{
					var usedSurfaceArea = node.width * node.height;
					if (node.left != null)
						usedSurfaceArea += UsedSurfaceArea(node.left);
					if (node.right != null)
						usedSurfaceArea += UsedSurfaceArea(node.right);

					return usedSurfaceArea;
				}

				// This is a leaf node, it doesn't constitute to the total surface area.
				return 0;
			}


			/// <summary>Inserts a new rectangle in the subtree rooted at the given node.</summary>
			private static Node Insert(Node node, int width, int height)
			{

				// If this node is an internal node, try both leaves for possible space.
				// (The rectangle in an internal node stores used space, the leaves store free space)
				if (node.left != null || node.right != null)
				{
					if (node.left != null)
					{
						var newNode = Insert(node.left, width, height);
						if (newNode != null)
							return newNode;
					}
					if (node.right != null)
					{
						var newNode = Insert(node.right, width, height);
						if (newNode != null)
							return newNode;
					}
					return null; // Didn't fit into either subtree!
				}

				// This node is a leaf, but can we fit the new rectangle here?
				if (width > node.width || height > node.height)
					return null; // Too bad, no space.

				// The new cell will fit, split the remaining space along the shorter axis,
				// that is probably more optimal.
				var w = node.width - width;
				var h = node.height - height;
				node.left = new();
				node.right = new();
				if (w <= h) // Split the remaining space in horizontal direction.
				{
					node.left.x = node.x + width;
					node.left.y = node.y;
					node.left.width = w;
					node.left.height = height;

					node.right.x = node.x;
					node.right.y = node.y + height;
					node.right.width = node.width;
					node.right.height = h;
				}
				else // Split the remaining space in vertical direction.
				{
					node.left.x = node.x;
					node.left.y = node.y + height;
					node.left.width = width;
					node.left.height = h;

					node.right.x = node.x + width;
					node.right.y = node.y;
					node.right.width = w;
					node.right.height = node.height;
				}
				// Note that as a result of the above, it can happen that node.left or node.right
				// is now a degenerate (zero area) rectangle. No need to do anything about it,
				// like remove the nodes as "unnecessary" since they need to exist as children of
				// this node (this node can't be a leaf anymore).

				// This node is now a non-leaf, so shrink its area - it now denotes
				// *occupied* space instead of free space. Its children spawn the resulting
				// area of free space.
				node.width = width;
				node.height = height;
				return node;
			}
		}
	}
}