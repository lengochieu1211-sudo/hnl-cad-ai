export interface AutoCadCommandKnowledge {
  id: string;
  command: string;
  aliases: string[];
  shortcut?: string;
  title: string;
  category: "DRAW" | "MODIFY" | "VIEW" | "SETUP" | "FILE" | "COORDINATE";
  description: string;
  workflow: string[];
  options?: string[];
  nativeAction?: string;
  standaloneNote?: string;
  sourcePages: string[];
}

export const AUTOCAD_2023_COMMAND_KNOWLEDGE: AutoCadCommandKnowledge[] = [
  {
    id:"AC23_UNITS", command:"UNITS", aliases:["UN","UNITS"], title:"Drawing Units", category:"SETUP",
    description:"Thiết lập kiểu đơn vị, độ chính xác, kiểu góc và insertion scale.",
    workflow:["UNITS","Chọn Length Type / Precision","Chọn Angle Type / Precision","Kiểm tra Insertion Scale","OK"],
    nativeAction:"UNITS", standaloneNote:"Standalone HNL mặc định mm.", sourcePages:["1-5","1-6"]
  },
  {
    id:"AC23_LIMITS", command:"LIMITS", aliases:["LIMITS"], title:"Drawing Limits", category:"SETUP",
    description:"Đặt giới hạn tham chiếu của vùng làm việc và Grid trong Model Space.",
    workflow:["LIMITS","Chọn lower-left hoặc Enter nhận mặc định","Chọn upper-right"],
    options:["On","Off"], nativeAction:"LIMITS", sourcePages:["1-6"]
  },
  {
    id:"AC23_ZOOM", command:"ZOOM", aliases:["Z","ZOOM"], title:"Zoom", category:"VIEW",
    description:"Điều khiển vùng nhìn; tài liệu minh họa Zoom All sau khi đặt Drawing Limits.",
    workflow:["Z hoặc ZOOM","Chọn option","A = All để fit đối tượng/limits"],
    options:["All","Extents","Window","Previous","Realtime"], nativeAction:"ZOOM", sourcePages:["1-7"]
  },
  {
    id:"AC23_LINE", command:"LINE", aliases:["L","LINE"], title:"Line", category:"DRAW",
    description:"Vẽ chuỗi đoạn thẳng liên tiếp cho đến khi kết thúc command.",
    workflow:["L / LINE","Specify first point","Specify next point","Tiếp tục chọn/nhập điểm","Enter kết thúc"],
    options:["Close","Undo"], nativeAction:"LINE", sourcePages:["1-8","1-9","1-19","1-21"]
  },
  {
    id:"AC23_ERASE", command:"ERASE", aliases:["E","ERASE"], title:"Erase", category:"MODIFY",
    description:"Xóa đối tượng theo workflow Select objects rồi xác nhận.",
    workflow:["E / ERASE","Select objects","Enter hoặc right-click xác nhận"],
    nativeAction:"ERASE", sourcePages:["1-13","1-33"]
  },
  {
    id:"AC23_PAN", command:"PAN", aliases:["P","PAN"], title:"Pan Realtime", category:"VIEW",
    description:"Dịch vùng nhìn mà không thay đổi hình học.",
    workflow:["P / PAN","Drag để dịch view","Esc để thoát"],
    nativeAction:"PAN", sourcePages:["1-20","1-21"]
  },
  {
    id:"AC23_CIRCLE", command:"CIRCLE", aliases:["C","CIRCLE"], title:"Circle", category:"DRAW",
    description:"Tạo Circle theo nhiều điều kiện hình học.",
    workflow:["C / CIRCLE","Chọn tâm hoặc option","Nhập Radius/Diameter hoặc các điểm theo option"],
    options:["Center, Radius","Center, Diameter","2P","3P","TTR","TTT"],
    nativeAction:"CIRCLE", sourcePages:["1-23","1-24","1-25"]
  },
  {
    id:"AC23_ARC", command:"ARC", aliases:["A","ARC"], title:"Arc", category:"DRAW",
    description:"AutoCAD hỗ trợ nhiều cách dựng Arc; tài liệu nhấn mạnh các cách dùng phổ biến.",
    workflow:["A / ARC","Chọn phương pháp dựng","Nhập các điểm theo prompt"],
    options:["3 Points","Center-Start-End"], nativeAction:"ARC", sourcePages:["1-33"]
  },
  {
    id:"AC23_GRID", command:"GRID", aliases:["GRID"], shortcut:"F7", title:"Grid Display", category:"VIEW",
    description:"Bật/tắt lưới tham chiếu trong vùng vẽ.",
    workflow:["F7 để toggle Grid Display","Hoặc dùng GRID/GRIDMODE native"],
    nativeAction:"GRID", sourcePages:["1-7","1-10","1-11"]
  },
  {
    id:"AC23_SNAP", command:"SNAP", aliases:["SNAP"], shortcut:"F9", title:"Snap Mode", category:"SETUP",
    description:"Khóa con trỏ theo bước Snap; Drafting Settings dùng để cấu hình Snap/Grid.",
    workflow:["F9 hoặc Status Bar để toggle Snap","Right-click Snap → Snap settings khi cần cấu hình"],
    nativeAction:"SNAP", sourcePages:["1-12","1-18","1-21"]
  },
  {
    id:"AC23_UCS", command:"UCS", aliases:["UCS"], title:"UCS / WCS", category:"COORDINATE",
    description:"Quản lý hệ tọa độ người dùng và quan hệ với World Coordinate System.",
    workflow:["Kiểm tra UCS icon","Dùng UCS để thay đổi hệ tọa độ khi cần"],
    nativeAction:"UCS", sourcePages:["1-15","1-16"]
  },
  {
    id:"AC23_POINT_INPUT", command:"POINT INPUT", aliases:["X,Y","@X,Y","@D<A"], title:"5 cách nhập điểm", category:"COORDINATE",
    description:"Các phương pháp nhập điểm nền tảng dùng chung cho lệnh vẽ.",
    workflow:[
      "Interactive: click điểm",
      "Absolute: X,Y",
      "Relative rectangular: @X,Y",
      "Relative polar: @Distance<Angle",
      "Direct Distance: chỉ hướng con trỏ rồi nhập khoảng cách"
    ],
    sourcePages:["1-18","1-21"]
  },
  {
    id:"AC23_SAVE", command:"QSAVE", aliases:["QSAVE","SAVE"], shortcut:"Ctrl+S", title:"Save DWG", category:"FILE",
    description:"Lưu bản vẽ; DWG là định dạng mặc định của AutoCAD.",
    workflow:["Ctrl+S hoặc Save","Chọn tên/đường dẫn khi cần","Lưu DWG"],
    nativeAction:"QSAVE", sourcePages:["1-25","1-36"]
  },
  {
    id:"AC23_QUIT", command:"QUIT", aliases:["QUIT"], shortcut:"Ctrl+Q", title:"Exit AutoCAD", category:"FILE",
    description:"Thoát AutoCAD và để AutoCAD xử lý prompt lưu nếu bản vẽ có thay đổi.",
    workflow:["Ctrl+Q hoặc QUIT","Xử lý Save prompt nếu có"],
    nativeAction:"QUIT", sourcePages:["1-36"]
  }
];

export const AUTOCAD_2023_COMMAND_MAP = new Map(
  AUTOCAD_2023_COMMAND_KNOWLEDGE.flatMap((item) =>
    item.aliases.map((alias) => [alias.toUpperCase(), item] as const)
  )
);

export function findAutoCad2023Command(input: string) {
  return AUTOCAD_2023_COMMAND_MAP.get(input.trim().toUpperCase()) || null;
}
