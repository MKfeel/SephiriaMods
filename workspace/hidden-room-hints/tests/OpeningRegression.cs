using System;
using System.Collections.Generic;
using System.Linq;
using SephiriaHiddenRoomHints;

static class OpeningRegression
{
    internal static void Run(Action<bool,string> check)
    {
        for (int dir=0;dir<4;dir++)
        {
            bool vertical = dir%2==0;
            var a=new Cell(10,20);var b=vertical?new Cell(10,24):new Cell(14,20);
            var cells=new HashSet<Cell>();
            check(OpeningGeometry.Library(a,b,dir,2,_=>true,cells) && cells.Count==(vertical?10:15),"native desert passage width direction "+dir);
            check(vertical ? cells.Contains(new Cell(11,24))&&!cells.Contains(new Cell(9,20))
                : cells.Contains(new Cell(14,18))&&!cells.Contains(new Cell(10,17)),"desert excludes preserved side walls direction "+dir);
            var reverse=new HashSet<Cell>();
            OpeningGeometry.Library(b,a,dir,2,_=>true,reverse);
            check(cells.SetEquals(reverse),"reversed endpoint order direction "+dir);
        }
        var existing=new HashSet<Cell>{new Cell(10,20),new Cell(11,20),new Cell(10,21),new Cell(9,20)};
        var output=new HashSet<Cell>();
        OpeningGeometry.Library(new Cell(10,20),new Cell(10,25),0,2,existing.Contains,output);
        check(output.Count==3 && !output.Contains(new Cell(9,20)),"only actual walls inside passage, no nearest-wall selection");
        check(OpeningGeometry.Boundary(output).Count==8,"L-shaped opening keeps concavity, no bounding rectangle");
        var hole=new HashSet<Cell>();for(int x=0;x<3;x++)for(int y=0;y<3;y++)if(x!=1||y!=1)hole.Add(new Cell(x,y));
        check(OpeningGeometry.Boundary(hole).Count==16,"existing empty center retains inner boundary");
        check(OpeningGeometry.Boundary(new HashSet<Cell>{new Cell(0,0),new Cell(1,0)}).Count==6,"shared cell edges not drawn");
        check(OpeningGeometry.Boundary(new HashSet<Cell>()).Count==0,"opened passage has no outline");
        check(!OpeningGeometry.Library(new Cell(0,0),new Cell(1,2),0,2,_=>true,new HashSet<Cell>()),"invalid non-axis passage rejected");
        for(int dir=0;dir<4;dir++)
        {
            int dx=dir==1?1:dir==3?-1:0,dy=dir==0?1:dir==2?-1:0;
            var lanes=dx==0?new[]{new Cell(0,0)}:new[]{new Cell(0,0),new Cell(0,1)};
            var walls=new HashSet<Cell>();
            foreach(var c in lanes){walls.Add(c);walls.Add(new Cell(c.X+dx,c.Y+dy));walls.Add(new Cell(c.X+3*dx,c.Y+3*dy));}
            var removed=new HashSet<Cell>();
            check(OpeningGeometry.Enhanced(lanes,dx,dy,walls.Contains,removed)&&removed.Count==lanes.Length*2,
                "enhanced stops at empty row before unrelated wall direction "+dir);
        }
        var uneven=new HashSet<Cell>{new Cell(0,0),new Cell(1,0),new Cell(1,1),new Cell(2,1),new Cell(4,0)};
        var removed2=new HashSet<Cell>();
        check(OpeningGeometry.Enhanced(new[]{new Cell(0,0),new Cell(0,1)},1,0,uneven.Contains,removed2)&&removed2.Count==4,
            "partially empty row continues until both lanes empty");
        var shared=new HashSet<Cell>{new Cell(0,0),new Cell(1,0),new Cell(2,0)};
        var preRemoved=new HashSet<Cell>{new Cell(0,0)};
        OpeningGeometry.Enhanced(new[]{new Cell(0,0)},1,0,shared.Contains,preRemoved);
        check(preRemoved.Count==1,"second opening call sees first side already removed");
    }
}
