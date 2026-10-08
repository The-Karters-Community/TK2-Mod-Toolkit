import json
from pathlib import Path
import struct
import tempfile
import unittest
from unittest import mock
from studio import model_assets as m

class ModelTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory(prefix='tk2-model-backend-'); self.root=Path(self.temp.name)
        self.source=self.root/'creator'; self.source.mkdir(); self.library=self.root/'library'
        self.patch=mock.patch.object(m,'LIBRARY',self.library);self.patch.start()
        self.obj=self.source/'cube.obj';self.obj.write_text('v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n')
    def tearDown(self):self.patch.stop();self.temp.cleanup()
    def test_import_preview(self):
        r=m.import_model(self.obj);self.assertEqual(r['vertices'],3);self.assertEqual(r['faces'],1)
        p=m.preview(r['id']);self.assertEqual(p['faces'],[[2,1,0]]);self.assertEqual(len(m.state()['models']),1)
    def test_reject_traversal(self):
        self.obj.write_text('mtllib ../outside.mtl\nv 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3')
        with self.assertRaises(ValueError):m.import_model(self.obj)
        self.assertEqual(m.state()['models'],[])
    def test_invalid_geometry(self):
        for invalid in ['v NaN 0 0\nf 1 1 1','v 0 0 0\nf 1 2 3','v 0 0 0\nf 0 1 1']:
            self.obj.write_text(invalid)
            with self.assertRaises(ValueError):m.import_model(self.obj)
    def test_texture_and_unchanged_plugin(self):
        png=b'\x89PNG\r\n\x1a\n'+b'\0'*8+struct.pack('>II',4,4)
        (self.source/'diffuse.png').write_bytes(png);(self.source/'body.mtl').write_text('newmtl body\nKd .3 .4 .5\nmap_Kd diffuse.png\n')
        self.obj.write_text('mtllib body.mtl\nv 0 0 0\nv 1 0 0\nv 0 1 0\nusemtl body\nf 1 2 3')
        r=m.import_model(self.obj);self.assertEqual(len(r['files']),3)
        game=self.root/'fake-game';game.mkdir();(game/'TheKarters2.exe').write_bytes(b'exe');(game/'GameAssembly.dll').write_bytes(b'game')
        plugin=game/'BepInEx/plugins/TK2.Customization.dll';plugin.parent.mkdir(parents=True);plugin.write_bytes(b'plugin')
        config=game/'BepInEx/config/local.tk2.customization.cfg';config.parent.mkdir();config.write_bytes(b'config')
        with mock.patch.object(m.core,'ROOT',self.root/'toolkit'):
            d=m.deploy(game,r['id'])
        self.assertEqual(d['modelPath'],r['id']+'/model.obj');self.assertEqual(d['files'],3);self.assertEqual(plugin.read_bytes(),b'plugin');self.assertEqual(config.read_bytes(),b'config')
    def test_hash_tamper_no_write(self):
        r=m.import_model(self.obj);(self.library/r['id']/'model.obj').write_text('changed')
        game=self.root/'fake-game';game.mkdir();(game/'TheKarters2.exe').write_bytes(b'exe');(game/'GameAssembly.dll').write_bytes(b'game')
        with self.assertRaises(ValueError):m.deploy(game,r['id'])
        self.assertFalse((game/'BepInEx').exists())
    def test_preview_sampling(self):
        self.obj.write_text('v 0 0 0\nv 1 0 0\nv 0 1 0\n'+'f 1 2 3\n'*2001);r=m.import_model(self.obj);p=m.preview(r['id'])
        self.assertEqual(len(p['faces']),2000);self.assertTrue(p['sampled']);self.assertEqual(p['sourceFaces'],2001)
    def test_bundle(self):
        bundle=self.source/'kart.bundle';bundle.write_bytes(b'UnityFS\0'+b'fixture')
        r=m.import_model(bundle);self.assertIn('unsupported',m.preview(r['id']));self.assertEqual(r['modelPath'],'model.bundle')
    def test_oversized_image(self):
        image=self.source/'bad.png';image.write_bytes(b'\x89PNG\r\n\x1a\n'+b'\0'*8+struct.pack('>II',65536,1))
        with self.assertRaises(ValueError):m._image_size(image)
    def test_absolute_dependency(self):
        for dependency in ['C:/private/foo.png','../private/foo.png','image.png:secret']:
            with self.assertRaises(ValueError):m._relative_file(self.source,dependency)

if __name__=='__main__':unittest.main()
