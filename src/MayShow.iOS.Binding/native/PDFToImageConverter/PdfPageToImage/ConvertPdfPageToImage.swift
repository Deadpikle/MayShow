//
//  PdfPageToImage.swift
//  PdfPageToImage
//

import Foundation
import PDFKit
import CoreGraphics

@objc(ConvertPdfPageToImage)
public class ConvertPdfPageToImage : NSObject
{
    // @objc
    // public static func sayHello() {
    //     print("hello")
    // }

    // @objc
    // public static func getString(myString: String) -> String {
    //     return myString  + " from swift!"
    // }
    
    @objc
    public static func getPdfPageCount(myPath: String) -> Int {
        let url = URL(fileURLWithPath: myPath)
        guard let document = PDFDocument(url: url) else { return -1 }
        return document.pageCount
    }

    @objc
    public static func convertPdfPageToImage(inputPath: String, outputPath: String, pageNum: Int) -> Int {
        // https://www.nutrient.io/blog/convert-pdf-to-image-in-swift/
        // print("Processing PDF at \(inputPath) -> \(outputPath) (page \(pageNum))")
        let url = URL(fileURLWithPath: inputPath)

        // Instantiate a `CGPDFDocument` from the PDF file's URL.
        guard let document = PDFDocument(url: url) else { return -1 }

        // print("There are this many pages in the PDF: \(document.pageCount)")
        // Get the first page of the PDF document.
        guard let page = document.page(at: pageNum) else { return -2 }
        
        // Fetch the page rect for the page we want to render.
        let pageRect = page.bounds(for: .mediaBox)

        let renderer = UIGraphicsImageRenderer(size: pageRect.size)
        let img = renderer.image { ctx in
            // Set and fill the background color.
            UIColor.white.set()
            ctx.fill(CGRect(x: 0, y: 0, width: pageRect.width, height: pageRect.height))

            // Translate the context so that we only draw the `cropRect`.
            ctx.cgContext.translateBy(x: -pageRect.origin.x, y: pageRect.size.height - pageRect.origin.y)

            // Flip the context vertically because the Core Graphics coordinate system starts from the bottom.
            ctx.cgContext.scaleBy(x: 1.0, y: -1.0)

            // Draw the PDF page.
            page.draw(with: .mediaBox, to: ctx.cgContext)
        }
        do {
            // print("Saving to \(outputPath)")
            try img.jpegData(compressionQuality: 95)?.write(to: URL(fileURLWithPath: outputPath))
            return 1
        } catch {
            // print("Printing to \(outputPath) failed")
            return -3
        }
    }

}
