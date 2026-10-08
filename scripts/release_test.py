"""Release checks that must fail before an irreversible publication."""

import io
import os
from pathlib import Path
import tempfile
import unittest
from argparse import Namespace
from unittest.mock import MagicMock, patch
import zipfile

import release


class ReleaseTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.folder = Path(self.temp.name)
        self.sha = "a" * 40
        self.info = {"version": "3.0.0-rc.1", "archive_version": "3.0.0.0-rc.1", "tag": "v3.0.0-rc.1",
                     "title": "Harmony 3.0.0-rc.1", "prerelease": True, "frameworks": ["net472", "net10.0"]}
        for variant, package_id in release.IDS.items():
            frameworks = ["netstandard2.0"] if variant == "Ref" else self.info["frameworks"]
            prefix = "ref" if variant == "Ref" else "lib"
            dependencies = '' if variant == "Ref" else '<dependency id="Lib.Harmony.Ref" version="3.0.0-rc.1" />'
            if variant == "Thin":
                dependencies += '<dependency id="MonoMod.Core" version="1.3.6" />'
            spec = f'''<package xmlns="urn:nuget"><metadata><id>{package_id}</id><version>3.0.0-rc.1</version>
<repository commit="{self.sha}" /><dependencies>{dependencies}</dependencies></metadata></package>'''
            with zipfile.ZipFile(self.folder / f"{package_id}.3.0.0-rc.1.nupkg", "w") as z:
                z.writestr(package_id + ".nuspec", spec)
                for framework in frameworks:
                    z.writestr(f"{prefix}/{framework}/0Harmony.dll", f"{variant}-{framework}")
            with zipfile.ZipFile(self.folder / f"Harmony-{variant}.3.0.0.0-rc.1.zip", "w") as z:
                for framework in frameworks:
                    z.writestr(f"{framework}/0Harmony.dll", f"{variant}-{framework}")

    def manifest(self):
        files = release.validate_files(self.folder, self.info, self.sha)
        result = {**self.info, "source_sha": self.sha, "files": files}
        release.save_manifest(self.folder, result)
        return result

    def test_current_files_only(self):
        (self.folder / "Lib.Harmony.2.4.2.nupkg").write_text("stale package")
        self.assertEqual(len(release.validate_files(self.folder, self.info, self.sha)), 6)

    def test_missing_variant_fails(self):
        (self.folder / "Lib.Harmony.Thin.3.0.0-rc.1.nupkg").unlink()
        with self.assertRaises(FileNotFoundError):
            self.manifest()

    def test_package_for_wrong_commit_fails(self):
        with self.assertRaisesRegex(ValueError, "source commit"):
            release.validate_files(self.folder, self.info, "b" * 40)

    def test_zip_from_other_build_fails(self):
        with zipfile.ZipFile(self.folder / "Harmony-Fat.3.0.0.0-rc.1.zip", "w") as z:
            z.writestr("net472/0Harmony.dll", "other build")
        with self.assertRaisesRegex(ValueError, "ZIP/package mismatch"):
            self.manifest()

    def test_modified_manifest_files_fail(self):
        manifest = self.manifest()
        manifest["files"][next(iter(manifest["files"]))] = "0" * 64
        release.save_manifest(self.folder, manifest)
        with patch.object(release, "version_info", return_value=self.info), patch.object(release, "command", return_value=self.sha):
            with self.assertRaisesRegex(ValueError, "checksum"):
                release.verify(self.folder)

    def test_wrong_version_fails(self):
        self.manifest()
        with patch.object(release, "version_info", return_value={**self.info, "version": "3.0.0-rc.2"}):
            with self.assertRaisesRegex(ValueError, "version mismatch"):
                release.verify(self.folder)

    def test_repository_signature_does_not_change_payload(self):
        file = self.folder / "Lib.Harmony.3.0.0-rc.1.nupkg"
        signed = io.BytesIO(file.read_bytes())
        with zipfile.ZipFile(signed, "a") as z:
            z.writestr(".signature.p7s", b"NuGet repository signature")
        self.assertEqual(release.package_contents(signed.getvalue()), release.package_contents(file.read_bytes()))

    def test_untrusted_ci_runs_fail(self):
        base = {"id": 123, "path": ".github/workflows/test.yml", "head_sha": self.sha, "conclusion": "success",
                "event": "push", "head_repository": {"full_name": "pardeike/Harmony"}}
        for overrides in ({"head_sha": "b" * 40}, {"conclusion": "failure"}, {"event": "pull_request"},
                          {"path": ".github/workflows/docs.yml"}, {"head_repository": {"full_name": "other/Harmony"}}):
            with self.subTest(overrides=overrides), patch.object(release, "api", return_value={**base, **overrides}):
                with self.assertRaisesRegex(ValueError, "trusted CI"):
                    release.successful_run("pardeike/Harmony", "test.yml", self.sha, 123)

    def test_changed_notes_fail_before_publication(self):
        manifest = self.manifest()
        manifest["notes_sha256"] = "0" * 64
        release.save_manifest(self.folder, manifest)
        (self.folder / "release-notes.md").write_text("changed release text")
        with patch.object(release, "version_info", return_value=self.info), patch.object(release, "command", return_value=self.sha):
            with self.assertRaisesRegex(ValueError, "notes changed"):
                release.verify(self.folder, prepared=True)

    def run_publish(self, public):
        manifest = {**self.manifest(), "repository": "pardeike/Harmony", "ci_run": 123}
        args = Namespace(directory=str(self.folder), repo="pardeike/Harmony", preparation_run="456")
        with patch.object(release, "verify", return_value=manifest), patch.object(release, "successful_run"), \
                patch.object(release, "gates"), patch.object(release, "github_state"), \
                patch.object(release, "public_package", side_effect=public), patch.object(release, "push_package") as push, \
                patch.object(release, "publish_github") as github:
            release.publish(args)
            return push.call_args_list, github.call_count

    def test_resume_identical_packages_without_reupload(self):
        def public(package_id, version):
            return (self.folder / f"{package_id}.{version}.nupkg").read_bytes()
        calls, github = self.run_publish(public)
        self.assertEqual(calls, [])
        self.assertEqual(github, 1)

    def test_upload_order_ref_fat_thin(self):
        queries = 0
        def public(package_id, version):
            nonlocal queries
            queries += 1
            return None if queries <= 3 else (self.folder / f"{package_id}.{version}.nupkg").read_bytes()
        calls, github = self.run_publish(public)
        self.assertEqual([call.args[0].name for call in calls],
                         [f"{package_id}.3.0.0-rc.1.nupkg" for package_id in release.IDS.values()])
        self.assertEqual(github, 1)

    def test_conflicting_public_package_stops_before_upload(self):
        archive = io.BytesIO()
        with zipfile.ZipFile(archive, "w") as z:
            z.writestr("different", b"different package")
        with patch.object(release, "push_package") as push:
            with self.assertRaisesRegex(ValueError, "choose a new version"):
                self.run_publish(lambda *_: archive.getvalue())
            push.assert_not_called()

    def test_nuget_upload_protocol_and_secret_stays_out_of_process_arguments(self):
        file = self.folder / "Lib.Harmony.3.0.0-rc.1.nupkg"
        response = MagicMock()
        response.__enter__.return_value.status = 201
        with patch.dict(os.environ, {"NUGET_API_KEY": "dummy-unit-test-key"}), \
                patch.object(release.urllib.request, "urlopen", return_value=response) as upload, \
                patch.object(release, "command") as process:
            release.push_package(file)
            request = upload.call_args.args[0]
            self.assertEqual(request.method, "PUT")
            self.assertEqual(request.get_header("X-nuget-protocol-version"), "4.1.0")
            self.assertIn(file.read_bytes(), request.data)
            self.assertNotIn(b"dummy-unit-test-key", request.data)
            process.assert_not_called()


if __name__ == "__main__":
    unittest.main()
