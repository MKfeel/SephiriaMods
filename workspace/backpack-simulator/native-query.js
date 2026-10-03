// Generated from installed StoneTablet.ParseQuery. Regenerate with tools/build_query_parser.py.
(function(root){
"use strict";
function ItemPosition(x,y){this.x=x;this.y=y;}
function AdditionMetadata(position,value){this.position=position;this.value=value;}
const GridInventory={IdxToPos:(i,w)=>new ItemPosition(i%w,Math.trunc(i/w)),PosToIdx:(x,y,w)=>y*w+x};
function parseQuery(query,width,height,storage,originPos,rotation)

	{
		
		const list = [];
		const array = query.split(/[\r\n]+/).filter(Boolean);
		for (let i = 0; i < array.length; i++)
		{
			let array2 = array[i].split(' ');
			switch (array2[0])
			{
			case "O":
				list.push(new AdditionMetadata(new ItemPosition(originPos.x, originPos.y), array2[1]));
				break;
			case "IDX":
			{
				let itemPosition = GridInventory.IdxToPos(Number(array2[1]), width);
				list.push(new AdditionMetadata(new ItemPosition(itemPosition.x, itemPosition.y), array2[2]));
				break;
			}
			case "RIDX":
			{
				let itemPosition2 = GridInventory.IdxToPos(storage - 1 - Number(array2[1]), width);
				list.push(new AdditionMetadata(new ItemPosition(itemPosition2.x, itemPosition2.y), array2[2]));
				break;
			}
			case "LEFT":
				switch (rotation)
				{
				case 0:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x - 1, originPos.y), array2[1]));
					break;
				case 1:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x, originPos.y + 1), array2[1]));
					break;
				case 2:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x + 1, originPos.y), array2[1]));
					break;
				case 3:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x, originPos.y - 1), array2[1]));
					break;
				}
				break;
			case "LEFTLEFT":
				switch (rotation)
				{
				case 0:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x - 2, originPos.y), array2[1]));
					break;
				case 1:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x, originPos.y + 2), array2[1]));
					break;
				case 2:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x + 2, originPos.y), array2[1]));
					break;
				case 3:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x, originPos.y - 2), array2[1]));
					break;
				}
				break;
			case "LEFTLEFTLEFT":
				switch (rotation)
				{
				case 0:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x - 3, originPos.y), array2[1]));
					break;
				case 1:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x, originPos.y + 3), array2[1]));
					break;
				case 2:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x + 3, originPos.y), array2[1]));
					break;
				case 3:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x, originPos.y - 3), array2[1]));
					break;
				}
				break;
			case "LEFTLEFTLEFTLEFT":
				switch (rotation)
				{
				case 0:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x - 4, originPos.y), array2[1]));
					break;
				case 1:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x, originPos.y + 4), array2[1]));
					break;
				case 2:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x + 4, originPos.y), array2[1]));
					break;
				case 3:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x, originPos.y - 4), array2[1]));
					break;
				}
				break;
			case "RIGHT":
				switch (rotation)
				{
				case 0:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x + 1, originPos.y), array2[1]));
					break;
				case 1:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x, originPos.y - 1), array2[1]));
					break;
				case 2:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x - 1, originPos.y), array2[1]));
					break;
				case 3:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x, originPos.y + 1), array2[1]));
					break;
				}
				break;
			case "RIGHTRIGHT":
				switch (rotation)
				{
				case 0:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x + 2, originPos.y), array2[1]));
					break;
				case 1:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x, originPos.y - 2), array2[1]));
					break;
				case 2:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x - 2, originPos.y), array2[1]));
					break;
				case 3:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x, originPos.y + 2), array2[1]));
					break;
				}
				break;
			case "RIGHTRIGHTRIGHT":
				switch (rotation)
				{
				case 0:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x + 3, originPos.y), array2[1]));
					break;
				case 1:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x, originPos.y - 3), array2[1]));
					break;
				case 2:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x - 3, originPos.y), array2[1]));
					break;
				case 3:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x, originPos.y + 3), array2[1]));
					break;
				}
				break;
			case "RIGHTRIGHTRIGHTRIGHT":
				switch (rotation)
				{
				case 0:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x + 4, originPos.y), array2[1]));
					break;
				case 1:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x, originPos.y - 4), array2[1]));
					break;
				case 2:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x - 4, originPos.y), array2[1]));
					break;
				case 3:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x, originPos.y + 4), array2[1]));
					break;
				}
				break;
			case "UP":
				switch (rotation)
				{
				case 0:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x, originPos.y - 1), array2[1]));
					break;
				case 1:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x - 1, originPos.y), array2[1]));
					break;
				case 2:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x, originPos.y + 1), array2[1]));
					break;
				case 3:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x + 1, originPos.y), array2[1]));
					break;
				}
				break;
			case "UPUP":
				switch (rotation)
				{
				case 0:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x, originPos.y - 2), array2[1]));
					break;
				case 1:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x - 2, originPos.y), array2[1]));
					break;
				case 2:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x, originPos.y + 2), array2[1]));
					break;
				case 3:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x + 2, originPos.y), array2[1]));
					break;
				}
				break;
			case "UPUPUP":
				switch (rotation)
				{
				case 0:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x, originPos.y - 3), array2[1]));
					break;
				case 1:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x - 3, originPos.y), array2[1]));
					break;
				case 2:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x, originPos.y + 3), array2[1]));
					break;
				case 3:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x + 3, originPos.y), array2[1]));
					break;
				}
				break;
			case "UPUPUPUP":
				switch (rotation)
				{
				case 0:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x, originPos.y - 4), array2[1]));
					break;
				case 1:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x - 4, originPos.y), array2[1]));
					break;
				case 2:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x, originPos.y + 4), array2[1]));
					break;
				case 3:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x + 4, originPos.y), array2[1]));
					break;
				}
				break;
			case "DOWN":
				switch (rotation)
				{
				case 0:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x, originPos.y + 1), array2[1]));
					break;
				case 1:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x + 1, originPos.y), array2[1]));
					break;
				case 2:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x, originPos.y - 1), array2[1]));
					break;
				case 3:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x - 1, originPos.y), array2[1]));
					break;
				}
				break;
			case "DOWNDOWN":
				switch (rotation)
				{
				case 0:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x, originPos.y + 2), array2[1]));
					break;
				case 1:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x + 2, originPos.y), array2[1]));
					break;
				case 2:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x, originPos.y - 2), array2[1]));
					break;
				case 3:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x - 2, originPos.y), array2[1]));
					break;
				}
				break;
			case "DOWNDOWNDOWN":
				switch (rotation)
				{
				case 0:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x, originPos.y + 3), array2[1]));
					break;
				case 1:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x + 3, originPos.y), array2[1]));
					break;
				case 2:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x, originPos.y - 3), array2[1]));
					break;
				case 3:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x - 3, originPos.y), array2[1]));
					break;
				}
				break;
			case "DOWNDOWNDOWNDOWN":
				switch (rotation)
				{
				case 0:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x, originPos.y + 4), array2[1]));
					break;
				case 1:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x + 4, originPos.y), array2[1]));
					break;
				case 2:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x, originPos.y - 4), array2[1]));
					break;
				case 3:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x - 4, originPos.y), array2[1]));
					break;
				}
				break;
			case "HORIZONTAL":
				switch (rotation)
				{
				case 0:
				case 2:
				{
					for (let num71 = 0; num71 < width; num71++)
					{
						if (originPos.x != num71)
						{
							let item52 = new AdditionMetadata(new ItemPosition(num71, originPos.y), array2[1]);
							if (num71 == 0)
							{
								item52.borderLeft = true;
							}
							if (num71 == width - 1)
							{
								item52.borderRight = true;
							}
							list.push(item52);
						}
					}
					break;
				}
				case 1:
				case 3:
				{
					for (let num70 = 0; num70 < height; num70++)
					{
						if (originPos.y != num70)
						{
							let item51 = new AdditionMetadata(new ItemPosition(originPos.x, num70), array2[1]);
							if (num70 == 0)
							{
								item51.borderTop = true;
							}
							if (num70 == height - 1)
							{
								item51.borderBottom = true;
							}
							list.push(item51);
						}
					}
					break;
				}
				}
				break;
			case "VERTICAL":
				switch (rotation)
				{
				case 0:
				case 2:
				{
					for (let num69 = 0; num69 < height; num69++)
					{
						if (originPos.y != num69)
						{
							let item50 = new AdditionMetadata(new ItemPosition(originPos.x, num69), array2[1]);
							if (num69 == 0)
							{
								item50.borderTop = true;
							}
							if (num69 == height - 1)
							{
								item50.borderBottom = true;
							}
							list.push(item50);
						}
					}
					break;
				}
				case 1:
				case 3:
				{
					for (let num68 = 0; num68 < width; num68++)
					{
						if (originPos.x != num68)
						{
							let item49 = new AdditionMetadata(new ItemPosition(num68, originPos.y), array2[1]);
							if (num68 == 0)
							{
								item49.borderLeft = true;
							}
							if (num68 == width - 1)
							{
								item49.borderRight = true;
							}
							list.push(item49);
						}
					}
					break;
				}
				}
				break;
			case "X_PLUS":
				switch (rotation)
				{
				case 0:
				{
					for (let num66 = originPos.x + 1; num66 < width; num66++)
					{
						let item47 = new AdditionMetadata(new ItemPosition(num66, originPos.y), array2[1]);
						if (num66 == width - 1)
						{
							item47.borderRight = true;
						}
						list.push(item47);
					}
					break;
				}
				case 1:
				{
					for (let num65 = 0; num65 < originPos.y; num65++)
					{
						let item46 = new AdditionMetadata(new ItemPosition(originPos.x, num65), array2[1]);
						if (num65 == 0)
						{
							item46.borderTop = true;
						}
						list.push(item46);
					}
					break;
				}
				case 2:
				{
					for (let num67 = 0; num67 < originPos.x; num67++)
					{
						let item48 = new AdditionMetadata(new ItemPosition(num67, originPos.y), array2[1]);
						if (num67 == 0)
						{
							item48.borderLeft = true;
						}
						list.push(item48);
					}
					break;
				}
				case 3:
				{
					for (let num64 = originPos.y; num64 < height; num64++)
					{
						let item45 = new AdditionMetadata(new ItemPosition(originPos.x, num64), array2[1]);
						if (num64 == height - 1)
						{
							item45.borderBottom = true;
						}
						list.push(item45);
					}
					break;
				}
				}
				break;
			case "X_MINUS":
				switch (rotation)
				{
				case 0:
				{
					for (let num62 = 0; num62 < originPos.x; num62++)
					{
						let item43 = new AdditionMetadata(new ItemPosition(num62, originPos.y), array2[1]);
						if (num62 == 0)
						{
							item43.borderLeft = true;
						}
						list.push(item43);
					}
					break;
				}
				case 1:
				{
					for (let num61 = originPos.y; num61 < height; num61++)
					{
						let item42 = new AdditionMetadata(new ItemPosition(originPos.x, num61), array2[1]);
						if (num61 == height - 1)
						{
							item42.borderBottom = true;
						}
						list.push(item42);
					}
					break;
				}
				case 2:
				{
					for (let num63 = originPos.x + 1; num63 < width; num63++)
					{
						let item44 = new AdditionMetadata(new ItemPosition(num63, originPos.y), array2[1]);
						if (num63 == width - 1)
						{
							item44.borderRight = true;
						}
						list.push(item44);
					}
					break;
				}
				case 3:
				{
					for (let num60 = 0; num60 < originPos.y; num60++)
					{
						let item41 = new AdditionMetadata(new ItemPosition(originPos.x, num60), array2[1]);
						if (num60 == 0)
						{
							item41.borderTop = true;
						}
						list.push(item41);
					}
					break;
				}
				}
				break;
			case "Y_PLUS":
				switch (rotation)
				{
				case 0:
				{
					for (let num58 = originPos.y; num58 < height; num58++)
					{
						let item39 = new AdditionMetadata(new ItemPosition(originPos.x, num58), array2[1]);
						if (num58 == height - 1)
						{
							item39.borderBottom = true;
						}
						list.push(item39);
					}
					break;
				}
				case 1:
				{
					for (let num57 = originPos.x + 1; num57 < width; num57++)
					{
						let item38 = new AdditionMetadata(new ItemPosition(num57, originPos.y), array2[1]);
						if (num57 == width - 1)
						{
							item38.borderRight = true;
						}
						list.push(item38);
					}
					break;
				}
				case 2:
				{
					for (let num59 = 0; num59 < originPos.y; num59++)
					{
						let item40 = new AdditionMetadata(new ItemPosition(originPos.x, num59), array2[1]);
						if (num59 == 0)
						{
							item40.borderTop = true;
						}
						list.push(item40);
					}
					break;
				}
				case 3:
				{
					for (let num56 = 0; num56 < originPos.x; num56++)
					{
						let item37 = new AdditionMetadata(new ItemPosition(num56, originPos.y), array2[1]);
						if (num56 == 0)
						{
							item37.borderLeft = true;
						}
						list.push(item37);
					}
					break;
				}
				}
				break;
			case "Y_MINUS":
				switch (rotation)
				{
				case 0:
				{
					for (let num54 = 0; num54 < originPos.y; num54++)
					{
						let item35 = new AdditionMetadata(new ItemPosition(originPos.x, num54), array2[1]);
						if (num54 == 0)
						{
							item35.borderTop = true;
						}
						list.push(item35);
					}
					break;
				}
				case 1:
				{
					for (let num53 = 0; num53 < originPos.x; num53++)
					{
						let item34 = new AdditionMetadata(new ItemPosition(num53, originPos.y), array2[1]);
						if (num53 == 0)
						{
							item34.borderLeft = true;
						}
						list.push(item34);
					}
					break;
				}
				case 2:
				{
					for (let num55 = originPos.y; num55 < height; num55++)
					{
						let item36 = new AdditionMetadata(new ItemPosition(originPos.x, num55), array2[1]);
						if (num55 == height - 1)
						{
							item36.borderBottom = true;
						}
						list.push(item36);
					}
					break;
				}
				case 3:
				{
					for (let num52 = originPos.x + 1; num52 < width; num52++)
					{
						let item33 = new AdditionMetadata(new ItemPosition(num52, originPos.y), array2[1]);
						if (num52 == width - 1)
						{
							item33.borderRight = true;
						}
						list.push(item33);
					}
					break;
				}
				}
				break;
			case "TOP":
				switch (rotation)
				{
				case 0:
				{
					for (let num50 = 0; num50 < width; num50++)
					{
						let item31 = new AdditionMetadata(new ItemPosition(num50, 0), array2[1]);
						item31.isYWorldPosition = true;
						item31.borderTop = true;
						if (num50 == 0)
						{
							item31.borderLeft = true;
						}
						if (num50 == width - 1)
						{
							item31.borderRight = true;
						}
						list.push(item31);
					}
					break;
				}
				case 1:
				{
					for (let num49 = 0; num49 < height; num49++)
					{
						let item30 = new AdditionMetadata(new ItemPosition(0, num49), array2[1]);
						item30.isXWorldPosition = true;
						item30.borderLeft = true;
						if (num49 == 0)
						{
							item30.borderTop = true;
						}
						if (num49 == height - 1)
						{
							item30.borderBottom = true;
						}
						list.push(item30);
					}
					break;
				}
				case 2:
				{
					for (let num51 = 0; num51 < width; num51++)
					{
						let position4 = GridInventory.IdxToPos(storage - num51 - 1, width);
						let item32 = new AdditionMetadata(position4, array2[1]);
						item32.isYWorldPosition = true;
						item32.borderBottom = true;
						if (position4.x == 0)
						{
							item32.borderLeft = true;
						}
						if (position4.x == width - 1)
						{
							item32.borderRight = true;
						}
						list.push(item32);
					}
					break;
				}
				case 3:
				{
					for (let num47 = 0; num47 < height; num47++)
					{
						if (GridInventory.PosToIdx(width - 1, num47, width) <= storage - 1)
						{
							let item29 = new AdditionMetadata(new ItemPosition(width - 1, num47), array2[1]);
							item29.isXWorldPosition = true;
							item29.borderRight = true;
							if (num47 == 0)
							{
								item29.borderTop = true;
							}
							let num48 = GridInventory.PosToIdx(width - 1, num47 + 1, width);
							if (num47 == height - 1 || num48 > storage - 1)
							{
								item29.borderBottom = true;
							}
							list.push(item29);
						}
					}
					break;
				}
				}
				break;
			case "BOTTOM":
				switch (rotation)
				{
				case 0:
				{
					for (let num45 = 0; num45 < width; num45++)
					{
						let position3 = GridInventory.IdxToPos(storage - num45 - 1, width);
						let item27 = new AdditionMetadata(position3, array2[1]);
						item27.isYWorldPosition = true;
						item27.borderBottom = true;
						if (position3.x == 0)
						{
							item27.borderLeft = true;
						}
						if (position3.x == width - 1)
						{
							item27.borderRight = true;
						}
						list.push(item27);
					}
					break;
				}
				case 1:
				{
					for (let num43 = 0; num43 < height; num43++)
					{
						if (GridInventory.PosToIdx(width - 1, num43, width) <= storage - 1)
						{
							let item26 = new AdditionMetadata(new ItemPosition(width - 1, num43), array2[1]);
							item26.isXWorldPosition = true;
							item26.borderRight = true;
							if (num43 == 0)
							{
								item26.borderTop = true;
							}
							let num44 = GridInventory.PosToIdx(width - 1, num43 + 1, width);
							if (num43 == height - 1 || num44 > storage - 1)
							{
								item26.borderBottom = true;
							}
							list.push(item26);
						}
					}
					break;
				}
				case 2:
				{
					for (let num46 = 0; num46 < width; num46++)
					{
						let item28 = new AdditionMetadata(new ItemPosition(num46, 0), array2[1]);
						item28.isYWorldPosition = true;
						item28.borderTop = true;
						if (num46 == 0)
						{
							item28.borderLeft = true;
						}
						if (num46 == width - 1)
						{
							item28.borderRight = true;
						}
						list.push(item28);
					}
					break;
				}
				case 3:
				{
					for (let num42 = 0; num42 < height; num42++)
					{
						let item25 = new AdditionMetadata(new ItemPosition(0, num42), array2[1]);
						item25.isXWorldPosition = true;
						item25.borderLeft = true;
						if (num42 == 0)
						{
							item25.borderTop = true;
						}
						if (num42 == height - 1)
						{
							item25.borderBottom = true;
						}
						list.push(item25);
					}
					break;
				}
				}
				break;
			case "LEFTEND":
				switch (rotation)
				{
				case 0:
				{
					for (let num39 = 0; num39 < height; num39++)
					{
						let item23 = new AdditionMetadata(new ItemPosition(0, num39), array2[1]);
						item23.isXWorldPosition = true;
						item23.borderLeft = true;
						if (num39 == 0)
						{
							item23.borderTop = true;
						}
						if (num39 == height - 1)
						{
							item23.borderBottom = true;
						}
						list.push(item23);
					}
					break;
				}
				case 1:
				{
					for (let num38 = 0; num38 < width; num38++)
					{
						let position2 = GridInventory.IdxToPos(storage - num38 - 1, width);
						let item22 = new AdditionMetadata(position2, array2[1]);
						item22.isYWorldPosition = true;
						item22.borderBottom = true;
						if (position2.x == 0)
						{
							item22.borderLeft = true;
						}
						if (position2.x == width - 1)
						{
							item22.borderRight = true;
						}
						list.push(item22);
					}
					break;
				}
				case 2:
				{
					for (let num40 = 0; num40 < height; num40++)
					{
						if (GridInventory.PosToIdx(width - 1, num40, width) <= storage - 1)
						{
							let item24 = new AdditionMetadata(new ItemPosition(width - 1, num40), array2[1]);
							item24.isXWorldPosition = true;
							item24.borderRight = true;
							if (num40 == 0)
							{
								item24.borderTop = true;
							}
							let num41 = GridInventory.PosToIdx(width - 1, num40 + 1, width);
							if (num40 == height - 1 || num41 > storage - 1)
							{
								item24.borderBottom = true;
							}
							list.push(item24);
						}
					}
					break;
				}
				case 3:
				{
					for (let num37 = 0; num37 < width; num37++)
					{
						let item21 = new AdditionMetadata(new ItemPosition(num37, 0), array2[1]);
						item21.isYWorldPosition = true;
						item21.borderTop = true;
						if (num37 == 0)
						{
							item21.borderLeft = true;
						}
						if (num37 == width - 1)
						{
							item21.borderRight = true;
						}
						list.push(item21);
					}
					break;
				}
				}
				break;
			case "RIGHTEND":
				switch (rotation)
				{
				case 0:
				{
					for (let num34 = 0; num34 < height; num34++)
					{
						if (GridInventory.PosToIdx(width - 1, num34, width) <= storage - 1)
						{
							let item19 = new AdditionMetadata(new ItemPosition(width - 1, num34), array2[1]);
							item19.isXWorldPosition = true;
							item19.borderRight = true;
							if (num34 == 0)
							{
								item19.borderTop = true;
							}
							let num35 = GridInventory.PosToIdx(width - 1, num34 + 1, width);
							if (num34 == height - 1 || num35 > storage - 1)
							{
								item19.borderBottom = true;
							}
							list.push(item19);
						}
					}
					break;
				}
				case 1:
				{
					for (let num33 = 0; num33 < width; num33++)
					{
						let item18 = new AdditionMetadata(new ItemPosition(num33, 0), array2[1]);
						item18.isYWorldPosition = true;
						item18.borderTop = true;
						if (num33 == 0)
						{
							item18.borderLeft = true;
						}
						if (num33 == width - 1)
						{
							item18.borderRight = true;
						}
						list.push(item18);
					}
					break;
				}
				case 2:
				{
					for (let num36 = 0; num36 < height; num36++)
					{
						let item20 = new AdditionMetadata(new ItemPosition(0, num36), array2[1]);
						item20.isXWorldPosition = true;
						item20.borderLeft = true;
						if (num36 == 0)
						{
							item20.borderTop = true;
						}
						if (num36 == height - 1)
						{
							item20.borderBottom = true;
						}
						list.push(item20);
					}
					break;
				}
				case 3:
				{
					for (let n = 0; n < width; n++)
					{
						let position = GridInventory.IdxToPos(storage - n - 1, width);
						let item17 = new AdditionMetadata(position, array2[1]);
						item17.isYWorldPosition = true;
						item17.borderBottom = true;
						if (position.x == 0)
						{
							item17.borderLeft = true;
						}
						if (position.x == width - 1)
						{
							item17.borderRight = true;
						}
						list.push(item17);
					}
					break;
				}
				}
				break;
			case "RIGHT_RISING":
				switch (rotation)
				{
				case 0:
				{
					let num29 = originPos.x + 1;
					let num30 = originPos.y - 1;
					while (num29 < width && num30 >= 0)
					{
						let item15 = new AdditionMetadata(new ItemPosition(num29, num30), array2[1]);
						if (num29 == width - 1)
						{
							item15.borderRight = true;
						}
						if (num30 == 0)
						{
							item15.borderTop = true;
						}
						list.push(item15);
						num29++;
						num30--;
					}
					break;
				}
				case 1:
				{
					let num27 = originPos.x - 1;
					let num28 = originPos.y - 1;
					while (num27 >= 0 && num28 >= 0)
					{
						let item14 = new AdditionMetadata(new ItemPosition(num27, num28), array2[1]);
						if (num27 == 0)
						{
							item14.borderLeft = true;
						}
						if (num28 == 0)
						{
							item14.borderTop = true;
						}
						list.push(item14);
						num27--;
						num28--;
					}
					break;
				}
				case 2:
				{
					let num31 = originPos.x - 1;
					let num32 = originPos.y + 1;
					while (num31 >= 0 && num32 < height)
					{
						let item16 = new AdditionMetadata(new ItemPosition(num31, num32), array2[1]);
						if (num31 == 0)
						{
							item16.borderLeft = true;
						}
						if (num32 == height - 1)
						{
							item16.borderBottom = true;
						}
						list.push(item16);
						num31--;
						num32++;
					}
					break;
				}
				case 3:
				{
					let num25 = originPos.x + 1;
					let num26 = originPos.y + 1;
					while (num25 < width && num26 < height)
					{
						let item13 = new AdditionMetadata(new ItemPosition(num25, num26), array2[1]);
						if (num25 == width - 1)
						{
							item13.borderRight = true;
						}
						if (num26 == height - 1)
						{
							item13.borderBottom = true;
						}
						list.push(item13);
						num25++;
						num26++;
					}
					break;
				}
				}
				break;
			case "RIGHT_FALLING":
				switch (rotation)
				{
				case 0:
				{
					let num21 = originPos.x + 1;
					let num22 = originPos.y + 1;
					while (num21 < width && num22 < height)
					{
						let item11 = new AdditionMetadata(new ItemPosition(num21, num22), array2[1]);
						if (num21 == width - 1)
						{
							item11.borderRight = true;
						}
						if (num22 == height - 1)
						{
							item11.borderBottom = true;
						}
						list.push(item11);
						num21++;
						num22++;
					}
					break;
				}
				case 1:
				{
					let num19 = originPos.x + 1;
					let num20 = originPos.y - 1;
					while (num19 < width && num20 >= 0)
					{
						let item10 = new AdditionMetadata(new ItemPosition(num19, num20), array2[1]);
						if (num19 == width - 1)
						{
							item10.borderRight = true;
						}
						if (num20 == 0)
						{
							item10.borderTop = true;
						}
						list.push(item10);
						num19++;
						num20--;
					}
					break;
				}
				case 2:
				{
					let num23 = originPos.x - 1;
					let num24 = originPos.y - 1;
					while (num23 >= 0 && num24 >= 0)
					{
						let item12 = new AdditionMetadata(new ItemPosition(num23, num24), array2[1]);
						if (num23 == 0)
						{
							item12.borderLeft = true;
						}
						if (num24 == 0)
						{
							item12.borderTop = true;
						}
						list.push(item12);
						num23--;
						num24--;
					}
					break;
				}
				case 3:
				{
					let num17 = originPos.x - 1;
					let num18 = originPos.y + 1;
					while (num17 >= 0 && num18 < height)
					{
						let item9 = new AdditionMetadata(new ItemPosition(num17, num18), array2[1]);
						if (num17 == 0)
						{
							item9.borderLeft = true;
						}
						if (num18 == height - 1)
						{
							item9.borderBottom = true;
						}
						list.push(item9);
						num17--;
						num18++;
					}
					break;
				}
				}
				break;
			case "LEFT_RISING":
				switch (rotation)
				{
				case 0:
				{
					let num13 = originPos.x - 1;
					let num14 = originPos.y - 1;
					while (num13 >= 0 && num14 >= 0)
					{
						let item7 = new AdditionMetadata(new ItemPosition(num13, num14), array2[1]);
						if (num13 == 0)
						{
							item7.borderLeft = true;
						}
						if (num14 == 0)
						{
							item7.borderTop = true;
						}
						list.push(item7);
						num13--;
						num14--;
					}
					break;
				}
				case 1:
				{
					let num11 = originPos.x - 1;
					let num12 = originPos.y + 1;
					while (num11 >= 0 && num12 < height)
					{
						let item6 = new AdditionMetadata(new ItemPosition(num11, num12), array2[1]);
						if (num11 == 0)
						{
							item6.borderLeft = true;
						}
						if (num12 == height - 1)
						{
							item6.borderBottom = true;
						}
						list.push(item6);
						num11--;
						num12++;
					}
					break;
				}
				case 2:
				{
					let num15 = originPos.x + 1;
					let num16 = originPos.y + 1;
					while (num15 < width && num16 < height)
					{
						let item8 = new AdditionMetadata(new ItemPosition(num15, num16), array2[1]);
						if (num15 == width - 1)
						{
							item8.borderRight = true;
						}
						if (num16 == height - 1)
						{
							item8.borderBottom = true;
						}
						list.push(item8);
						num15++;
						num16++;
					}
					break;
				}
				case 3:
				{
					let num9 = originPos.x + 1;
					let num10 = originPos.y - 1;
					while (num9 < width && num10 >= 0)
					{
						let item5 = new AdditionMetadata(new ItemPosition(num9, num10), array2[1]);
						if (num9 == width - 1)
						{
							item5.borderRight = true;
						}
						if (num10 == 0)
						{
							item5.borderTop = true;
						}
						list.push(item5);
						num9++;
						num10--;
					}
					break;
				}
				}
				break;
			case "LEFT_FALLING":
				switch (rotation)
				{
				case 0:
				{
					let num5 = originPos.x - 1;
					let num6 = originPos.y + 1;
					while (num5 >= 0 && num6 < height)
					{
						let item3 = new AdditionMetadata(new ItemPosition(num5, num6), array2[1]);
						if (num5 == 0)
						{
							item3.borderLeft = true;
						}
						if (num6 == height - 1)
						{
							item3.borderBottom = true;
						}
						list.push(item3);
						num5--;
						num6++;
					}
					break;
				}
				case 1:
				{
					let num3 = originPos.x + 1;
					let num4 = originPos.y + 1;
					while (num3 < width && num4 < height)
					{
						let item2 = new AdditionMetadata(new ItemPosition(num3, num4), array2[1]);
						if (num3 == width - 1)
						{
							item2.borderRight = true;
						}
						if (num4 == height - 1)
						{
							item2.borderBottom = true;
						}
						list.push(item2);
						num3++;
						num4++;
					}
					break;
				}
				case 2:
				{
					let num7 = originPos.x + 1;
					let num8 = originPos.y - 1;
					while (num7 < width && num8 >= 0)
					{
						let item4 = new AdditionMetadata(new ItemPosition(num7, num8), array2[1]);
						if (num7 == width - 1)
						{
							item4.borderRight = true;
						}
						if (num8 == 0)
						{
							item4.borderTop = true;
						}
						list.push(item4);
						num7++;
						num8--;
					}
					break;
				}
				case 3:
				{
					let num = originPos.x - 1;
					let num2 = originPos.y - 1;
					while (num >= 0 && num2 >= 0)
					{
						let item = new AdditionMetadata(new ItemPosition(num, num2), array2[1]);
						if (num == 0)
						{
							item.borderLeft = true;
						}
						if (num2 == 0)
						{
							item.borderTop = true;
						}
						list.push(item);
						num--;
						num2--;
					}
					break;
				}
				}
				break;
			case "KNIGHTUPLEFT":
				switch (rotation)
				{
				case 0:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x - 1, originPos.y - 2), array2[1]));
					break;
				case 1:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x - 2, originPos.y + 1), array2[1]));
					break;
				case 2:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x + 1, originPos.y + 2), array2[1]));
					break;
				case 3:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x + 2, originPos.y - 1), array2[1]));
					break;
				}
				break;
			case "KNIGHTUPRIGHT":
				switch (rotation)
				{
				case 0:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x + 2, originPos.y - 1), array2[1]));
					break;
				case 1:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x - 1, originPos.y - 2), array2[1]));
					break;
				case 2:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x - 2, originPos.y + 1), array2[1]));
					break;
				case 3:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x + 1, originPos.y + 2), array2[1]));
					break;
				}
				break;
			case "KNIGHTDOWNLEFT":
				switch (rotation)
				{
				case 0:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x - 2, originPos.y + 1), array2[1]));
					break;
				case 1:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x + 1, originPos.y + 2), array2[1]));
					break;
				case 2:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x + 2, originPos.y - 1), array2[1]));
					break;
				case 3:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x - 1, originPos.y - 2), array2[1]));
					break;
				}
				break;
			case "KNIGHTDOWNRIGHT":
				switch (rotation)
				{
				case 0:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x + 1, originPos.y + 2), array2[1]));
					break;
				case 1:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x + 2, originPos.y - 1), array2[1]));
					break;
				case 2:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x - 1, originPos.y - 2), array2[1]));
					break;
				case 3:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x - 2, originPos.y + 1), array2[1]));
					break;
				}
				break;
			case "KNIGHTUPLEFT_INVERT":
				switch (rotation)
				{
				case 0:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x - 2, originPos.y - 1), array2[1]));
					break;
				case 1:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x - 1, originPos.y + 2), array2[1]));
					break;
				case 2:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x + 2, originPos.y + 1), array2[1]));
					break;
				case 3:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x + 1, originPos.y - 2), array2[1]));
					break;
				}
				break;
			case "KNIGHTDOWNLEFT_INVERT":
				switch (rotation)
				{
				case 0:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x - 1, originPos.y + 2), array2[1]));
					break;
				case 1:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x + 2, originPos.y + 1), array2[1]));
					break;
				case 2:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x + 1, originPos.y - 2), array2[1]));
					break;
				case 3:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x - 2, originPos.y - 1), array2[1]));
					break;
				}
				break;
			case "KNIGHTDOWNRIGHT_INVERT":
				switch (rotation)
				{
				case 0:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x + 2, originPos.y + 1), array2[1]));
					break;
				case 1:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x + 1, originPos.y - 2), array2[1]));
					break;
				case 2:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x - 2, originPos.y - 1), array2[1]));
					break;
				case 3:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x - 1, originPos.y + 2), array2[1]));
					break;
				}
				break;
			case "KNIGHTUPRIGHT_INVERT":
				switch (rotation)
				{
				case 0:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x + 1, originPos.y - 2), array2[1]));
					break;
				case 1:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x - 2, originPos.y - 1), array2[1]));
					break;
				case 2:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x - 1, originPos.y + 2), array2[1]));
					break;
				case 3:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x + 2, originPos.y + 1), array2[1]));
					break;
				}
				break;
			case "DIAUPLEFT":
				switch (rotation)
				{
				case 0:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x - 1, originPos.y - 1), array2[1]));
					break;
				case 1:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x - 1, originPos.y + 1), array2[1]));
					break;
				case 2:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x + 1, originPos.y + 1), array2[1]));
					break;
				case 3:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x + 1, originPos.y - 1), array2[1]));
					break;
				}
				break;
			case "DIADOWNLEFT":
				switch (rotation)
				{
				case 3:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x - 1, originPos.y - 1), array2[1]));
					break;
				case 0:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x - 1, originPos.y + 1), array2[1]));
					break;
				case 1:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x + 1, originPos.y + 1), array2[1]));
					break;
				case 2:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x + 1, originPos.y - 1), array2[1]));
					break;
				}
				break;
			case "DIAUPRIGHT":
				switch (rotation)
				{
				case 1:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x - 1, originPos.y - 1), array2[1]));
					break;
				case 2:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x - 1, originPos.y + 1), array2[1]));
					break;
				case 3:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x + 1, originPos.y + 1), array2[1]));
					break;
				case 0:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x + 1, originPos.y - 1), array2[1]));
					break;
				}
				break;
			case "DIADOWNRIGHT":
				switch (rotation)
				{
				case 0:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x + 1, originPos.y + 1), array2[1]));
					break;
				case 1:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x + 1, originPos.y - 1), array2[1]));
					break;
				case 2:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x - 1, originPos.y - 1), array2[1]));
					break;
				case 3:
					list.push(new AdditionMetadata(new ItemPosition(originPos.x - 1, originPos.y + 1), array2[1]));
					break;
				}
				break;
			case "CHECKERBOARD":
			{
				for (let l = 0; l < width; l++)
				{
					for (let m = 0; m < height; m++)
					{
						if ((originPos.x != l || originPos.y != m) && GridInventory.PosToIdx(l, m, width) <= storage - 1 && (l + m + originPos.x + originPos.y) % 2 == 0)
						{
							list.push(new AdditionMetadata(new ItemPosition(l, m), array2[1]));
						}
					}
				}
				break;
			}
			case "CHECKERBOARD2":
			{
				for (let j = 0; j < width; j++)
				{
					for (let k = 0; k < height; k++)
					{
						if ((originPos.x != j || originPos.y != k) && GridInventory.PosToIdx(j, k, width) <= storage - 1 && (j + k + originPos.x + originPos.y) % 2 == 1)
						{
							list.push(new AdditionMetadata(new ItemPosition(j, k), array2[1]));
						}
					}
				}
				break;
			}
			}
		}
		return list;
	}

if(typeof module!=="undefined"&&module.exports)module.exports=parseQuery;
else root.NativeTabletQuery=parseQuery;
})(typeof window!=="undefined"?window:globalThis);
