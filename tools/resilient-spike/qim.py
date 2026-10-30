import numpy as np, io, sys
from PIL import Image
rng = np.random.default_rng(1)
N = 8
C = np.array([[np.cos(np.pi*(2*n+1)*k/16) for n in range(8)] for k in range(8)])*np.sqrt(2/8)
C[0] /= np.sqrt(2)
COEFS = [(0,1),(1,0),(1,1),(0,2),(2,0),(1,2),(2,1),(2,2)]

def carrier(kind, S=1080):
    if kind == 'static':
        return rng.integers(30, 226, (S,S,3)).astype(np.float64)
    y,x = np.mgrid[0:S,0:S]
    base = np.stack([128+90*np.sin(x/90), 128+90*np.sin(y/70), 128+90*np.sin((x+y)/110)],-1)
    blocks = rng.integers(-40,40,(S//54,S//54,3)).repeat(54,0).repeat(54,1)
    return np.clip(base+blocks, 25, 230)

def to_blocks(Y):
    h,w = Y.shape
    return Y.reshape(h//8,8,w//8,8).transpose(0,2,1,3)
def from_blocks(B):
    h,w = B.shape[:2]
    return B.transpose(0,2,1,3).reshape(h*8,w*8)
def dct(B): return C @ B @ C.T
def idct(D): return C.T @ D @ C

def luma(img): return img @ np.array([0.299,0.587,0.114])

def embed(img, bits_per_site, delta, nco):
    Y = luma(img); B = to_blocks(Y); D = dct(B)
    cs = COEFS[:nco]
    nb = D.shape[0]*D.shape[1]
    sites = nb*nco
    bits = bits_per_site  # array length sites
    Df = D.reshape(nb,8,8)
    k=0
    out = Df.copy()
    for i,(u,v) in enumerate(cs):
        c = Df[:,u,v]; b = bits[i*nb:(i+1)*nb]
        off = b*delta/2
        out[:,u,v] = np.round((c-off)/delta)*delta+off
    Dn = out.reshape(D.shape)
    Yn = from_blocks(idct(Dn))
    res = img + (Yn-Y)[...,None]
    return np.clip(np.round(res),0,255)

def extract(img, delta, nco):
    Y = luma(img); D = dct(to_blocks(Y)); nb=D.shape[0]*D.shape[1]
    Df = D.reshape(nb,8,8); soft=[]
    for (u,v) in COEFS[:nco]:
        c = Df[:,u,v]
        d0 = np.abs(c-np.round(c/delta)*delta)
        d1 = np.abs(c-(np.round((c-delta/2)/delta)*delta+delta/2))
        soft.append((d0-d1)/delta)   # >0 => bit 1
    return np.concatenate(soft)

def jpeg(img,q,sub=True):
    buf=io.BytesIO(); Image.fromarray(img.astype(np.uint8)).save(buf,'JPEG',quality=q,subsampling=2 if sub else 0)
    return np.asarray(Image.open(buf)).astype(np.float64), buf.tell()
def webp(img,q):
    buf=io.BytesIO(); Image.fromarray(img.astype(np.uint8)).save(buf,'WEBP',quality=q)
    return np.asarray(Image.open(buf).convert('RGB')).astype(np.float64), buf.tell()

def psnr(a,b): return 10*np.log10(255**2/np.mean((a-b)**2))

if __name__=='__main__':
    kind = sys.argv[1]
    img = carrier(kind)
    nco = 8
    nb = (1080//8)**2
    sites = nb*nco
    bits = rng.integers(0,2,sites)
    print(f"carrier={kind} blocks={nb} sites={sites}")
    for delta in (24,32,48,64):
        emb = embed(img,bits,delta,nco)
        print(f"-- delta={delta}  PSNR(embed)={psnr(img,emb):.1f} dB  clean BER={np.mean((extract(emb,delta,nco)>0)!=bits):.4f}")
        for name,fn in [('jpeg95',lambda x:jpeg(x,95)),('jpeg85',lambda x:jpeg(x,85)),('jpeg75',lambda x:jpeg(x,75)),('jpeg60',lambda x:jpeg(x,60)),('jpeg50',lambda x:jpeg(x,50)),('jpeg40',lambda x:jpeg(x,40)),('webp75',lambda x:webp(x,75)),('webp50',lambda x:webp(x,50))]:
            out,sz = fn(emb)
            soft = extract(out,delta,nco)
            ber = np.mean((soft>0)!=bits)
            # repetition-r soft combining: BER of majority/soft sum over r sites
            res=[]
            for r in (4,8,16):
                m = (sites//r)*r
                s = (soft[:m]*np.where(bits[:m]==1,1,-1)).reshape(-1,r)  # signed correctness proxy per site
                # soft combine assumes the same bit repeated r times: emulate by sign-aware sum
                res.append(np.mean(s.sum(1)<=0))
            print(f"   {name:7s} size={sz/1024:6.0f}KB rawBER={ber:.4f}  repBER r4={res[0]:.4f} r8={res[1]:.4f} r16={res[2]:.4f}")
