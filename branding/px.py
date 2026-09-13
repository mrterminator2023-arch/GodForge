import random, math
random.seed(7)
W=H=96
px={}  # (x,y)->color
def put(x,y,c):
    if 0<=x<W and 0<=y<H: px[(x,y)]=c
def rect(x,y,w,h,c):
    for i in range(w):
        for j in range(h): put(x+i,y+j,c)

# --- background: dark vertical gradient bands
bands=["#07060b","#0b0810","#100b12","#150e13","#1a1113","#1f1413"]
for y in range(H):
    c=bands[min(len(bands)-1,int(y/H*len(bands)))]
    rect(0,y,W,1,c)

# --- fire: value-noise-ish flames behind anvil
def flame_field():
    # heat map: bottom hot, columns with random tongues
    heat=[[0.0]*W for _ in range(H)]
    for x in range(W):
        for y in range(H):
            base=(y/H)  # 0 top -> 1 bottom
            n=0
            for k,(f,a) in enumerate([(0.07,1.0),(0.15,0.6),(0.33,0.35)]):
                n+=a*math.sin(x*f*6.28+k*1.7+ y*0.05)*math.cos(y*f*3.1+k*0.9)
            n=n/1.95
            tongue=math.exp(-((x-48)/34.0)**2)
            v=base*1.15+n*0.32+tongue*0.32-0.2 + random.uniform(-0.03,0.03)
            heat[y][x]=v
    return heat
heat=flame_field()
def firecol(v):
    if v<0.55: return None
    if v<0.68: return "#5a0d0a"
    if v<0.78: return "#a3210f"
    if v<0.88: return "#e8551a"
    if v<0.98: return "#ffa028"
    if v<1.12: return "#ffd84a"
    return "#fff6c8"
for y in range(H):
    for x in range(W):
        c=firecol(heat[y][x])
        if c: put(x,y,c)
# sparks
for _ in range(45):
    x=random.randint(6,90); y=random.randint(4,70)
    put(x,y,random.choice(["#ffd84a","#ffa028","#fff6c8"]))

# (anvil removed)
# --- text: 5x7 font
F={
'G':["01110","10001","10000","10111","10001","10001","01111"],
'O':["01110","10001","10001","10001","10001","10001","01110"],
'D':["11110","10001","10001","10001","10001","10001","11110"],
'F':["11111","10000","10000","11110","10000","10000","10000"],
'R':["11110","10001","10001","11110","10100","10010","10001"],
'E':["11111","10000","10000","11110","10000","10000","11111"],
}
def text(s,x0,y0,scale):
    grad=["#fff6c8","#ffe98a","#ffd84a","#ffb130","#ff8a1e","#ff6a14","#e84a0f"]
    x=x0
    for ch in s:
        g=F[ch]
        for r,row in enumerate(g):
            for c,b in enumerate(row):
                if b=="1":
                    for i in range(scale):
                        for j in range(scale):
                            X=x+c*scale+i; Y=y0+r*scale+j
                            put(X,Y,grad[r])
        x+=5*scale+scale
    # outline (dark) around all text pixels
    w=len(s)*6*scale
def outline(s,x0,y0,scale,col="#1a0806"):
    cells=set()
    x=x0
    for ch in s:
        for r,row in enumerate(F[ch]):
            for c,b in enumerate(row):
                if b=="1":
                    for i in range(scale):
                        for j in range(scale): cells.add((x+c*scale+i,y0+r*scale+j))
        x+=5*scale+scale
    for (cx,cy) in list(cells):
        for dx in (-1,0,1):
            for dy in (-1,0,1):
                p=(cx+dx,cy+dy)
                if p not in cells: put(p[0],p[1],col)
    # drop shadow
    for (cx,cy) in cells:
        for d in (1,2):
            p=(cx+d,cy+d)
            if p not in cells: put(p[0],p[1],"#3a0f08")
S=3
gw=3*6*S-S; fw=5*6*S-S
gx=(W-gw)//2; fx=(W-fw)//2
outline("GOD",gx,22,S); outline("FORGE",fx,46,S)
text("GOD",gx,22,S); text("FORGE",fx,46,S)

# --- emit svg
out=['<svg xmlns="http://www.w3.org/2000/svg" width="2048" height="2048" viewBox="0 0 %d %d" shape-rendering="crispEdges">'%(W,H)]
for (x,y),c in px.items():
    out.append('<rect x="%d" y="%d" width="1" height="1" fill="%s"/>'%(x,y,c))
out.append('</svg>')
open("ava2.html","w").write('<html><head><meta charset="utf-8"><style>html,body{margin:0;background:#000}</style></head><body>'+"".join(out)+'</body></html>')
