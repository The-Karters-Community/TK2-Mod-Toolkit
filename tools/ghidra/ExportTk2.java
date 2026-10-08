// Export existing analysis without altering the program.
// @category TK2
import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.*;
import ghidra.program.model.listing.*;
import java.nio.file.*;
import java.io.*;
import java.nio.charset.StandardCharsets;

public class ExportTk2 extends GhidraScript {
    private String quoted(String s) {
        return "\"" + s.replace("\\", "\\\\").replace("\"", "\\\"")
            .replace("\r", "\\r").replace("\n", "\\n").replace("\t", "\\t") + "\"";
    }
    public void run() throws Exception {
        String[] args = getScriptArgs();
        Path out = Paths.get(args[0]);
        Files.createDirectories(out.resolve("pseudocode"));
        String filter = args.length > 1 ? args[1] : "PixelKartPhysics|Ant_MainGame|HpBarController|KartersLeaderboardsManager|PixelGameKartCamera|Ant_KartInput|Ant_BoostManager|PTK_Audio";
        if (filter.startsWith("@")) filter = String.join("|", Files.readAllLines(Paths.get(filter.substring(1))));
        int limit = args.length > 2 ? Integer.parseInt(args[2]) : 30;
        java.util.regex.Pattern pattern = java.util.regex.Pattern.compile(filter);
        DecompInterface decompiler = new DecompInterface();
        decompiler.openProgram(currentProgram);
        long functions = 0, named = 0, exported = 0, failed = 0;
        try (BufferedWriter index = Files.newBufferedWriter(out.resolve("functions.jsonl"), StandardCharsets.UTF_8)) {
            FunctionIterator iterator = currentProgram.getFunctionManager().getFunctions(true);
            while (iterator.hasNext() && !monitor.isCancelled()) {
                Function f = iterator.next();
                functions++;
                String name = f.getName(true);
                if (!f.getName().startsWith("FUN_")) named++;
                String addr = f.getEntryPoint().toString();
                index.write("{\"name\":" + quoted(name) + ",\"address\":" + quoted(addr)
                    + ",\"signature\":" + quoted(f.getSignature().toString()) + "}\n");
                if (exported + failed < limit && pattern.matcher(name).find()) {
                    DecompileResults r = decompiler.decompileFunction(f, 20, monitor);
                    String file = addr + "_" + name.replaceAll("[^A-Za-z0-9_.-]", "_");
                    if (file.length() > 150) file = file.substring(0, 150);
                    if (r.decompileCompleted() && r.getDecompiledFunction() != null) {
                        Files.writeString(out.resolve("pseudocode").resolve(file + ".c"),
                            "/* Ghidra pseudocode, not original C#; " + name + " @ " + addr + " */\n"
                            + r.getDecompiledFunction().getC(), StandardCharsets.UTF_8);
                        exported++;
                    } else {
                        Files.writeString(out.resolve("pseudocode").resolve(file + ".error.txt"), r.getErrorMessage());
                        failed++;
                    }
                }
            }
        } finally { decompiler.dispose(); }
        String summary = "{\"program\":" + quoted(currentProgram.getName())
            + ",\"sha256\":" + quoted(currentProgram.getExecutableSHA256())
            + ",\"imageBase\":" + quoted(currentProgram.getImageBase().toString())
            + ",\"language\":" + quoted(currentProgram.getLanguageID().toString())
            + ",\"functions\":" + functions + ",\"namedFunctions\":" + named
            + ",\"exported\":" + exported + ",\"failed\":" + failed
            + ",\"cancelled\":" + monitor.isCancelled() + "}";
        Files.writeString(out.resolve("summary.json"), summary, StandardCharsets.UTF_8);
        println(summary);
    }
}
