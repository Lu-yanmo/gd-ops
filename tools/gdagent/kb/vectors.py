"""vectors.bin 编解码（纯函数）：self-describing 头 + float32 载荷 + checksum。

文件布局：
    [b'GDKB'][u32 小端 头长][header JSON(UTF-8)][payload: float32 小端 行主序 chunk_count×dim]

header 记全"能不能信这份向量"所需的元信息（kb-plan P0-2/§1）：
    format_version / embed_model_name / dim / chunk_count / dtype / endian /
    normalized / created_ts / payload_sha256

search 启动时拿 header 的 embed_model_name+dim+normalized 与当前配置比对，不符就拒算
（拒绝拿两种空间的向量混算）。payload_sha256 覆盖载荷字节，读写各验一次，损坏当场暴露。
"""
import hashlib
import json
import struct
import time

MAGIC = b"GDKB"
FORMAT_VERSION = 1
DTYPE = "float32"
ENDIAN = "little"
_ROW = struct.Struct("<f")


class VectorFileError(Exception):
    pass


def encode(vectors, embed_model_name, dim, normalized=False, created_ts=None):
    """float 列表(或元组列表) → vectors.bin 字节。所有向量长度必须等于 dim。"""
    vectors = list(vectors)
    n = len(vectors)
    for i, v in enumerate(vectors):
        if len(v) != dim:
            raise VectorFileError("第 %d 条向量长度 %d ≠ dim %d" % (i, len(v), dim))
    parts = []
    pack = _ROW.pack
    for v in vectors:
        parts.append(b"".join(pack(float(x)) for x in v))
    payload = b"".join(parts)
    header = {
        "format_version": FORMAT_VERSION,
        "embed_model_name": str(embed_model_name),
        "dim": int(dim),
        "chunk_count": int(n),
        "dtype": DTYPE,
        "endian": ENDIAN,
        "normalized": bool(normalized),
        "created_ts": int(created_ts if created_ts is not None else time.time()),
        "payload_sha256": hashlib.sha256(payload).hexdigest(),
    }
    hb = json.dumps(header, ensure_ascii=True, sort_keys=True).encode("utf-8")
    return MAGIC + struct.pack("<I", len(hb)) + hb + payload


def decode(data):
    """vectors.bin 字节 → (header, rows)；rows 为 list[list[float]]，逐字节校验 checksum。"""
    data = bytes(data)
    if len(data) < 8 or data[:4] != MAGIC:
        raise VectorFileError("不是 KB 向量文件(magic 不符)")
    (hlen,) = struct.unpack_from("<I", data, 4)
    if len(data) < 8 + hlen:
        raise VectorFileError("文件头被截断")
    try:
        header = json.loads(data[8:8 + hlen].decode("utf-8"))
    except (ValueError, UnicodeDecodeError) as e:
        raise VectorFileError("文件头不是合法 JSON：%s" % e)
    payload = data[8 + hlen:]
    dim = int(header.get("dim") or 0)
    count = int(header.get("chunk_count") or 0)
    if dim <= 0 or count < 0:
        raise VectorFileError("文件头 dim/chunk_count 非法")
    if len(payload) != dim * count * 4:
        raise VectorFileError(
            "载荷长度 %d ≠ dim*count*4=%d(文件损坏或半写)" % (len(payload), dim * count * 4))
    if hashlib.sha256(payload).hexdigest() != header.get("payload_sha256"):
        raise VectorFileError("载荷 checksum 不符(文件被改过或损坏)")
    if header.get("endian") != ENDIAN or header.get("dtype") != DTYPE:
        raise VectorFileError("不支持的 dtype/endian：%s/%s" % (header.get("dtype"), header.get("endian")))
    flat = struct.unpack_from("<%df" % (dim * count), payload, 0) if count else ()
    rows = [list(flat[i * dim:(i + 1) * dim]) for i in range(count)]
    return header, rows


def model_matches(header, embed_model_name, dim=None):
    """search 前置校验：模型名(和可选维度)与配置一致才允许算。返回 (bool, 原因)。"""
    if str(header.get("embed_model_name")) != str(embed_model_name):
        return False, "索引向量由 %r 生成，当前配置是 %r —— 请 kb rebuild" % (
            header.get("embed_model_name"), embed_model_name)
    if dim is not None and int(header.get("dim") or -1) != int(dim):
        return False, "索引维度 %s ≠ 配置期望 %s" % (header.get("dim"), dim)
    return True, ""
