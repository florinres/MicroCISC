
using CPU.Business.Models;
using ICSharpCode.AvalonEdit;
using Microsoft.CodeAnalysis;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows.Media;
using Ui.Components;
using Ui.Interfaces.Services;
using Ui.Interfaces.ViewModel;
using Ui.Models;
using Ui.ViewModels.Components.Microprogram;
using Ui.ViewModels.Generics;

namespace Ui.Services;

public class CpuService : ICpuService
{
    private readonly CPU.Business.CPU _cpu;
    private bool _cursorNeedsUpdate = false;
    private readonly IMicroprogramViewModel _microprogramService;
    private readonly IDiagramViewModel _diagram;
    private Dictionary<int, int> _mirLookUpIndex = new Dictionary<int, int>
    {
        {0, 0},
        {1, 1},
        {2, 5},
        {3, 2},
        {4, 3},
        {5, 4},
        {6, 6}
    };
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        WriteIndented = true
    };
    public static List<ISR>? Isrs;
    public List<MemorySection> MemorySections { get; set; }
    private IActiveDocumentService _activeDocumentService;

    public CpuService(IMicroprogramViewModel microprogramService, CPU.Business.CPU cpu, IDiagramViewModel diagram, IActiveDocumentService activeDocumentService)
    {
        _cpu = cpu;
        _diagram = diagram;
        _microprogramService = microprogramService;
        _activeDocumentService = activeDocumentService;
        _ = LoadJsonMpm();

        Isrs = ReadIVTJson();
        InitMemorySections();
    }

    public async Task LoadJsonMpm(string filePath = "", bool debug = false)
    {
        string fullPath;
        
        if (filePath == "")
        {
            var projectRoot = AppContext.BaseDirectory + "../../../";
            fullPath = Path.Combine(projectRoot, "../Configs", "Mpm.json");
        }
        else
        {
            fullPath = filePath;
        }

        var jsonString = await File.ReadAllTextAsync(fullPath);
        //both can be paralized if necesarry
        _microprogramService.LoadMicroprogramFromJson(jsonString);
        _cpu.LoadJsonMpm(jsonString);
    }

    public void StepMicrocommand()
    {
        _diagram.ResetHighlight();

        var (row, column) = _cpu.StepMicrocommand();

        if (row == 0 && column == 0)
        {
            _microprogramService.ClearAllHighlightedRows();
            _cursorNeedsUpdate = true;
        }

        _microprogramService.CurrentRow = row;
        _microprogramService.CurrentColumn = _mirLookUpIndex[column];
        Brush highlightColour = null;
        if (_microprogramService.CurrentMemoryRow[_mirLookUpIndex[column]].Value.Contains("Pm", StringComparison.OrdinalIgnoreCase))
            highlightColour = Brushes.Red;
        else if (_microprogramService.CurrentMemoryRow[_mirLookUpIndex[column]].Value.Contains("Pd", StringComparison.OrdinalIgnoreCase))
            highlightColour = Brushes.Blue;

        // Update bus values in diagram
        _diagram.UpdateBusValues(_cpu.SBUS, _cpu.DBUS, _cpu.RBUS);

        _diagram.HandleHighlightConnection(_cpu.Registers[REGISTERS.FLAGS], _microprogramService.CurrentMemoryRow[_mirLookUpIndex[column]].Value, true, highlightColour);

        _diagram.HandleHighlightMpmBox(_mirLookUpIndex[column]);

        UpdateEditorAndHighlight(_cpu.Registers[REGISTERS.PC], _cursorNeedsUpdate);

        _cursorNeedsUpdate = false;
    }
    public void StepMicroinstruction()
    {
        int row, col;
        row = 1;
        col = 1;

        _diagram.ResetHighlight();

        _cursorNeedsUpdate = false;

        (row, col) = _cpu.StepMicrocommand();

        if (row == 0)
        {
            _cursorNeedsUpdate = true;
            _microprogramService.ClearAllHighlightedRows();
        }

        _microprogramService.CurrentRow = row;
        _microprogramService.CurrentColumn = -1;
        Brush highlightColour = null;
        if (_microprogramService.CurrentMemoryRow[_mirLookUpIndex[col]].Value.Contains("Pm", StringComparison.OrdinalIgnoreCase))
            highlightColour = Brushes.Red;
        else if (_microprogramService.CurrentMemoryRow[_mirLookUpIndex[col]].Value.Contains("Pd", StringComparison.OrdinalIgnoreCase))
            highlightColour = Brushes.Blue;

        // Update bus values in diagram
        _diagram.UpdateBusValues(_cpu.SBUS, _cpu.DBUS, _cpu.RBUS);

        _diagram.HandleHighlightConnection(_cpu.Registers[REGISTERS.FLAGS], _microprogramService.CurrentMemoryRow[_mirLookUpIndex[col]].Value, true, highlightColour);

        if (row == 97 && col == 2)
        {
            UpdateEditorAndHighlight((ushort)(_cpu.Registers[REGISTERS.PC] - 2), true);
        }
        else
        {
            UpdateEditorAndHighlight(_cpu.Registers[REGISTERS.PC], _cursorNeedsUpdate);
        }

        while ((col != 6) && (row != 97 || col != 2))
        {
            (row, col) = _cpu.StepMicrocommand();

            _microprogramService.CurrentRow = row;
            _microprogramService.CurrentColumn = -1;

            // Update bus values in diagram
            _diagram.UpdateBusValues(_cpu.SBUS, _cpu.DBUS, _cpu.RBUS);

            _diagram.HandleHighlightConnection(_cpu.Registers[REGISTERS.FLAGS], _microprogramService.CurrentMemoryRow[_mirLookUpIndex[col]].Value, true, highlightColour);
        }
    }
    public void StepInstruction()
    {
        int row, col;
        row = 1;
        col = 1;

        _diagram.ResetHighlight();

        (row, col) = _cpu.StepMicrocommand();

        _microprogramService.CurrentRow = row;
        _microprogramService.CurrentColumn = _mirLookUpIndex[col];
        Brush colour = null;
        if (_microprogramService.CurrentMemoryRow[_mirLookUpIndex[col]].Value.Contains("Pm", StringComparison.OrdinalIgnoreCase))
            colour = Brushes.Red;
        else if (_microprogramService.CurrentMemoryRow[_mirLookUpIndex[col]].Value.Contains("Pd", StringComparison.OrdinalIgnoreCase))
            colour = Brushes.Blue;

        // Update bus values in diagram
        _diagram.UpdateBusValues(_cpu.SBUS, _cpu.DBUS, _cpu.RBUS);

        _diagram.HandleHighlightConnection(_cpu.Registers[REGISTERS.FLAGS], _microprogramService.CurrentMemoryRow[_mirLookUpIndex[col]].Value, true, colour);

        // Check if CPU is accessing flags in each microcommand
        while ((row != 0 || col != 0) && (row != 97 || col != 2))
        {
            (row, col) = _cpu.StepMicrocommand();

            _microprogramService.CurrentRow = row;
            _microprogramService.CurrentColumn = _mirLookUpIndex[col];
            if (_microprogramService.CurrentMemoryRow[_mirLookUpIndex[col]].Value.Contains("Pm", StringComparison.OrdinalIgnoreCase))
                colour = Brushes.Red;
            else if (_microprogramService.CurrentMemoryRow[_mirLookUpIndex[col]].Value.Contains("Pd", StringComparison.OrdinalIgnoreCase))
                colour = Brushes.Blue;

            // Update bus values in diagram
            _diagram.UpdateBusValues(_cpu.SBUS, _cpu.DBUS, _cpu.RBUS);

            _diagram.HandleHighlightConnection(_cpu.Registers[REGISTERS.FLAGS], _microprogramService.CurrentMemoryRow[_mirLookUpIndex[col]].Value, true, colour);
        }

        if (row == 97 && col == 2)
        {
            UpdateEditorAndHighlight((ushort)(_cpu.Registers[REGISTERS.PC] - 2), true);
        }
        else
        {
            _microprogramService.ClearAllHighlightedRows();
            UpdateEditorAndHighlight(_cpu.Registers[REGISTERS.PC], true);
        }

    }
    public void ResetProgram()
    {
        foreach (var doc in _activeDocumentService.Documents)
        {
            doc.ResetHighlight();
        }

        _cpu.ResetProgram();
        _diagram.ResetHighlight();
        _microprogramService.CurrentRow = -1;
        _microprogramService.CurrentColumn = -1;
        _microprogramService.ClearAllHighlightedRows();
    }
    public void StartDebugging()
    {
        ushort lineNum = 0;

        _activeDocumentService.SelectedDocument?.IsBeingDebugged = true;

        lineNum = GetLineAndEditorNumberByPc(_cpu.Registers[REGISTERS.PC]);

        _activeDocumentService.SelectedDocument?.HighlightLine(lineNum);
    }
    public void TriggerInterrupt(ISR isr)
    {
        switch (isr.Name)
        { 
            case "ACLOW":
                _cpu.SetInterruptFlag(1);
                _cpu.Registers[Exceptions.ACLOW] = true;
                break;
            case "CIL":
                _cpu.SetInterruptFlag(1);
                _cpu.Registers[Exceptions.CIL] = true;
                break;
            case "Reserved0":
                _cpu.SetInterruptFlag(1);
                _cpu.Registers[Exceptions.Reserved0] = true;
                break;
            case "Reserved1":
                _cpu.SetInterruptFlag(1);
                _cpu.Registers[Exceptions.Reserved1] = true;
                break;
            case "IRQ0":
                _diagram.SetIOColor(Brushes.Red, "IO0");
                _cpu.Registers[IRQs.IRQ0] = true;
                break;
            case "IRQ1":
                _diagram.SetIOColor(Brushes.Red, "IO1");
                _cpu.Registers[IRQs.IRQ1] = true;
                break;
            case "IRQ2":
                _diagram.SetIOColor(Brushes.Red, "IO2");
                _cpu.Registers[IRQs.IRQ2] = true;
                break;
            case "IRQ3":
                _diagram.SetIOColor(Brushes.Red, "IO3");
                _cpu.Registers[IRQs.IRQ3] = true;
                break;
        }
    }
    public void StopDebugging()
    {
        foreach (var doc in _activeDocumentService.Documents)
        {
            doc.ResetHighlight();
        }
        _activeDocumentService.SelectedDocument?.IsBeingDebugged = false;
        _diagram.ResetHighlight();
        _cpu.ResetProgram();
        _microprogramService.CurrentRow = -1;
        _microprogramService.CurrentColumn = -1;
        _microprogramService.ClearAllHighlightedRows();
    }
    public void UpdateDebugSymbols(string code, Dictionary<ushort, ushort> debugSymbols, ushort sectionAddress)
    {
        foreach (var memorySection in MemorySections)
        {
            if (memorySection.StartAddress == sectionAddress)
            {
                memorySection.Code = code;
                memorySection.DebugSymbols = debugSymbols;
                break;
            }
        }
    }
    private void InitMemorySections()
    {
        MemorySections = new List<MemorySection>
        {
            new MemorySection("IVT",        0x0000, 0x000F, new Dictionary<ushort,ushort>(), ""),
            new MemorySection("User_Code",  0x0010, 0x555F, new Dictionary<ushort,ushort>(), ""),
            new MemorySection("ACLOW",      0x5560, 0x6009, new Dictionary<ushort,ushort>(), ""),
            new MemorySection("CIL",        0x600A, 0x6AB3, new Dictionary<ushort,ushort>(), ""),
            new MemorySection("Reserved0",  0x6AB4, 0x755D, new Dictionary<ushort,ushort>(), ""),
            new MemorySection("Reserved1",  0x755E, 0x8007, new Dictionary<ushort,ushort>(), ""),
            new MemorySection("IRQ0",       0x8008, 0x8AB1, new Dictionary<ushort,ushort>(), ""),
            new MemorySection("IRQ1",       0x8AB2, 0x955B, new Dictionary<ushort,ushort>(), ""),
            new MemorySection("IRQ2",       0x955C, 0xA005, new Dictionary<ushort,ushort>(), ""),
            new MemorySection("IRQ3",       0xA006, 0xAAAF, new Dictionary<ushort,ushort>(), ""),
            new MemorySection("Stack",      0xAAB0, 0xFFFF, new Dictionary<ushort,ushort>(), ""),
        };
    }
    private List<ISR> ReadIVTJson()
    {
        string currentFolder = Path.GetFullPath(AppContext.BaseDirectory + "../../../../");
        string jsonPath = Path.Combine(currentFolder + "Configs", "IVT.json");
        if (!File.Exists(jsonPath))
            return new List<ISR>();
        string json = File.ReadAllText(jsonPath);

        return JsonSerializer.Deserialize<List<ISR>>(json, JsonOpts) ?? new();
    }
    void ChangEditorBasedOnSection(MemorySection section)
    {
        if (_activeDocumentService.SelectedDocument == null) return;

        foreach (var doc in _activeDocumentService.Documents)
        {
            if ((section.Name == "User_Code") && (doc.IsBeingDebugged == true))
            {
                _activeDocumentService.SelectedDocument = doc;
                return;
            }
        }

        // If the document is already open, select it
        foreach (var doc in _activeDocumentService.Documents)
        {

            if (doc.SectionName == section.Name)
            {
                _activeDocumentService.SelectedDocument = doc;
                return;
            }
        }
        
        if(IsSectionIsr(section))
        {
            FileViewModel isrFile = new FileViewModel
            {
                Title = section.Name,
                Content = section.Code,
                SectionName = section.Name
            };

            _activeDocumentService.Documents.Add(isrFile);
            _activeDocumentService.SelectedDocument = isrFile;
        }
    }
    private ushort GetLineAndEditorNumberByPc(ushort pc)
    {
        MemorySection? section = GetMemorySectionByAddress(pc);

        if (section == null || _activeDocumentService.SelectedDocument == null)
            return 0;

        ChangEditorBasedOnSection(section);

        if (section != null && section.DebugSymbols != null && section.DebugSymbols.ContainsKey(pc))
            return section.DebugSymbols[pc];

        return 0;
    }
    private MemorySection? GetMemorySectionByAddress(ushort address)
    {
        foreach (var section in MemorySections)
        {
            if (IsAddressInSection(address, section))
                return section;
        }
        return null;
    }
    private bool IsAddressInSection(ushort address, MemorySection section)
    {
        return address >= section.StartAddress && address <= section.EndAddress;
    }
    private void UpdateEditorAndHighlight(ushort pc, bool cursorNeedsUpdate)
    {
        ushort lineNum = GetLineAndEditorNumberByPc(pc);

        if (cursorNeedsUpdate)
            _activeDocumentService.SelectedDocument?.HighlightLine(lineNum);
    }
    private bool IsSectionIsr(MemorySection section)
    {
        if (Isrs == null) return false;

        foreach(var isr in Isrs)
        {
            if (isr.Name == section.Name)
                return true;
        }

        return false;
    }
    public ushort GetIR(){
        return (ushort)_cpu.Registers[REGISTERS.IR];
    }
}