# Resilient-mode feasibility spike

Throwaway Python experiment (numpy + Pillow) behind the numbers in
[`docs/resilient-mode-plan.md`](../../docs/resilient-mode-plan.md). It is not
part of the product and is not shipped.

It embeds random bits into the 8x8 DCT coefficients of an image's luma
channel using quantization index modulation (QIM), pushes the result through
JPEG and WebP at several qualities, and reports the bit error rate.

```bash
pip install numpy pillow
python3 qim.py gradient   # smooth carrier
python3 qim.py static     # noise carrier (worst case for JPEG)
```

The "repBER" columns are a rough proxy for soft-combined repetition coding,
not a real ECC decoder. Treat all results as order-of-magnitude evidence that
the idea works, not as a specification.
